using System.Collections.Generic;
using System.Threading;
using YooAsset;
using Cysharp.Threading.Tasks;
using GameLogic.Data;
using TEngine;
using UnityEngine;

namespace GameLogic.GamePlay.CorePlay
{
    /// <summary>
    /// 关卡数据加载与解析
    /// </summary>
    public class LevelDataConfigParse
    {
        private const string LevelDataResPath = "TextLevelDataScriptableObject";
        private const string GraphicDataResPath = "TextGraphicDataScriptableObject";

        private TextLevelDataScriptableObject _levelDataAsset;
        private TextGraphicDataScriptableObject _graphicDataAsset;
        private readonly Dictionary<string, string> _characterChunks = new Dictionary<string, string>();
        private readonly Dictionary<string, AssetHandle> _chunkHandles = new Dictionary<string, AssetHandle>();
        private readonly LinkedList<string> _chunkLru = new LinkedList<string>();
        private readonly SemaphoreSlim _graphicLoadGate = new SemaphoreSlim(1, 1);
        private const int MaxCachedChunks = 2;
        private bool _released;
        private TextGraphicData _activeGraphicData;

        /// <summary>所有关卡数据</summary>
        public List<TextLevelData> AllLevels { get; private set; }

        /// <summary>关卡名称 -> 关卡数据</summary>
        public Dictionary<string, TextLevelData> LevelNameMap { get; private set; }

        /// <summary>levelId -> levelName</summary>
        public Dictionary<int, string> LevelIdToNameMap { get; private set; }

        /// <summary>字形数据映射（字符 -> 图形数据）</summary>
        public Dictionary<string, TextGraphicData> GraphicDataMap { get; private set; }

        /// <summary>音调数据映射（字符 -> 音调）</summary>
        public Dictionary<string, string> CharacterToToneMap { get; private set; }


        /// <summary>加载所有关卡配置</summary>
        public async UniTask LoadAllLevels()
        {
            _levelDataAsset = await GameModule.Resource.LoadAssetAsync<TextLevelDataScriptableObject>(LevelDataResPath);
            _graphicDataAsset = await GameModule.Resource.LoadAssetAsync<TextGraphicDataScriptableObject>(GraphicDataResPath);

            // 构建 LevelName -> TextLevelData 映射
            AllLevels = new List<TextLevelData>();
            LevelNameMap = new Dictionary<string, TextLevelData>();
            CharacterToToneMap = new Dictionary<string, string>();
            foreach (var textToneData in _levelDataAsset.characterToTone)
            {
                CharacterToToneMap.Add(textToneData.character,textToneData.tone);
            }
            if (_levelDataAsset != null && _levelDataAsset.levelDataList != null)
            {
                foreach (var level in _levelDataAsset.levelDataList)
                {
                    if (level != null && !string.IsNullOrEmpty(level.levelName))
                    {
                        AllLevels.Add(level);
                        LevelNameMap[level.levelName] = level;
                    }
                }
                Log.Info($"加载了 {AllLevels.Count} 个关卡数据");
            }
            else
            {
                Log.Error($"未找到关卡数据: Resources/{LevelDataResPath}");
            }

            // 构建 LevelId -> LevelName 映射（使用 ConfigSystem 的 TbLevel 表）
            LevelIdToNameMap = new Dictionary<int, string>();
            var tbLevel = ConfigSystem.Instance.Tables.TbLevel;
            if (tbLevel != null && tbLevel.DataMap != null && tbLevel.DataMap.Count > 0)
            {
                foreach (var kv in tbLevel.DataMap)
                {
                    int levelId = kv.Key;
                    string levelName = kv.Value?.LevelName;
                    if (levelId > 0 && !string.IsNullOrEmpty(levelName))
                    {
                        LevelIdToNameMap[levelId] = levelName;
                    }
                }
                Log.Info($"从 TbLevel 加载了 {LevelIdToNameMap.Count} 个关卡表条目");
            }
            else
            {
                Log.Warning("TbLevel 表为空或不可用");
            }

            BuildGraphicDataMap();
        }

        private void BuildGraphicDataMap()
        {
            GraphicDataMap = new Dictionary<string, TextGraphicData>();
            _characterChunks.Clear();
            if (_graphicDataAsset == null) return;
            // 兼容尚未拆分的旧配置，新的主文件只含地址索引。
            if (_graphicDataAsset.TextGraphicDataList != null)
                foreach (var data in _graphicDataAsset.TextGraphicDataList)
                    if (data != null && !string.IsNullOrEmpty(data.character)) GraphicDataMap[data.character] = data;
            foreach (var entry in _graphicDataAsset.GraphicChunks)
                foreach (char character in entry.characters)
                    _characterChunks.Add(character.ToString(), entry.resourceName);
        }

        /// <summary>按需读取所在分片，保留最近两个分片。并发请求串行化，避免重复加载。</summary>
        public async UniTask<TextGraphicData> GetGraphicDataAsync(string character)
        {
            if (_released || string.IsNullOrEmpty(character)) return null;
            await _graphicLoadGate.WaitAsync();
            try
            {
                if (_released) return null;
                if (GraphicDataMap.TryGetValue(character, out var cached))
                {
                    if (_characterChunks.TryGetValue(character, out var cachedName)) TouchChunk(cachedName);
                    return cached;
                }
                if (!_characterChunks.TryGetValue(character, out var name)) return null;
                var handle = GameModule.Resource.LoadAssetAsyncHandle<TextGraphicDataScriptableObject>(name);
                bool retained = false;
                try
                {
                    await handle.ToUniTask();
                    if (_released) return null;
                    var chunk = handle.AssetObject as TextGraphicDataScriptableObject;
                    if (chunk == null || chunk.TextGraphicDataList == null) return null;
                    _chunkHandles.Add(name, handle);
                    retained = true;
                    foreach (var data in chunk.TextGraphicDataList) GraphicDataMap[data.character] = data;
                    TouchChunk(name);
                    while (_chunkLru.Count > MaxCachedChunks) EvictChunk(_chunkLru.First.Value);
                    return GetGraphicData(character);
                }
                finally
                {
                    if (!retained) handle.Dispose();
                }
            }
            finally { _graphicLoadGate.Release(); }
        }

        private void TouchChunk(string name)
        {
            _chunkLru.Remove(name);
            _chunkLru.AddLast(name);
        }

        private void EvictChunk(string name)
        {
            if (!_chunkHandles.TryGetValue(name, out var handle)) return;
            var chunk = handle.AssetObject as TextGraphicDataScriptableObject;
            if (chunk != null && chunk.TextGraphicDataList != null)
                foreach (var data in chunk.TextGraphicDataList) GraphicDataMap.Remove(data.character);
            _chunkHandles.Remove(name);
            _chunkLru.Remove(name);
            handle.Dispose();
        }

        public void ReleaseGraphicCache()
        {
            _released = true;
            _activeGraphicData = null;
            while (_chunkLru.Count > 0) EvictChunk(_chunkLru.First.Value);
            GraphicDataMap?.Clear();
        }

        /// <summary>根据关卡名称获取关卡数据</summary>
        public TextLevelData GetLevelData(string levelName)
        {
            if (LevelNameMap == null || string.IsNullOrEmpty(levelName))
                return null;
            LevelNameMap.TryGetValue(levelName, out var data);
            return data;
        }

        /// <summary>根据 levelId 获取关卡数据（1开始）</summary>
        public TextLevelData GetLevelDataByLevelId(int levelId)
        {
            if (LevelIdToNameMap == null) return null;
            if (!LevelIdToNameMap.TryGetValue(levelId, out string levelName))
                return null;
            return GetLevelData(levelName);
        }

        /// <summary>根据 levelId 获取关卡名称</summary>
        public string GetLevelNameByLevelId(int levelId)
        {
            if (LevelIdToNameMap == null) return null;
            LevelIdToNameMap.TryGetValue(levelId, out string name);
            return name;
        }

        // 当前已显示的字保留一份托管数据引用，快速跳关的旧请求不能影响全选。
        public void SetActiveGraphicData(TextGraphicData data) => _activeGraphicData = data;

        /// <summary>获取已加载字符的图形数据；冷加载使用 GetGraphicDataAsync。</summary>
        public TextGraphicData GetGraphicData(string character)
        {
            if (_activeGraphicData != null && _activeGraphicData.character == character) return _activeGraphicData;
            if (GraphicDataMap == null || string.IsNullOrEmpty(character))
                return null;
            GraphicDataMap.TryGetValue(character, out var data);
            return data;
        }

        /// <summary>获取关卡总数（按关卡表）</summary>
        public int LevelCount => LevelIdToNameMap?.Count ?? 0;

        /// <summary>获取最大 levelId</summary>
        public int MaxLevelId
        {
            get
            {
                if (LevelIdToNameMap == null || LevelIdToNameMap.Count == 0) return 0;
                int max = 0;
                foreach (var id in LevelIdToNameMap.Keys)
                {
                    if (id > max) max = id;
                }
                return max;
            }
        }
        
    }
}