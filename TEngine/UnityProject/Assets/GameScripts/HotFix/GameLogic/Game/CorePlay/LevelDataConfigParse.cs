using System.Collections.Generic;
using GameLogic.Data;
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

        /// <summary>所有关卡数据</summary>
        public List<TextLevelData> AllLevels { get; private set; }

        /// <summary>关卡名称 -> 关卡数据</summary>
        public Dictionary<string, TextLevelData> LevelNameMap { get; private set; }

        /// <summary>levelId -> levelName</summary>
        public Dictionary<int, string> LevelIdToNameMap { get; private set; }

        /// <summary>字形数据映射（字符 -> 图形数据）</summary>
        public Dictionary<string, TextGraphicData> GraphicDataMap { get; private set; }

        /// <summary>加载所有关卡配置</summary>
        public void LoadAllLevels()
        {
            _levelDataAsset = GameModule.Resource.LoadAsset<TextLevelDataScriptableObject>(LevelDataResPath);
            _graphicDataAsset = GameModule.Resource.LoadAsset<TextGraphicDataScriptableObject>(GraphicDataResPath);

            // 构建 LevelName -> TextLevelData 映射
            AllLevels = new List<TextLevelData>();
            LevelNameMap = new Dictionary<string, TextLevelData>();
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
                Debug.Log($"加载了 {AllLevels.Count} 个关卡数据");
            }
            else
            {
                Debug.LogError($"未找到关卡数据: Resources/{LevelDataResPath}");
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
                Debug.Log($"从 TbLevel 加载了 {LevelIdToNameMap.Count} 个关卡表条目");
            }
            else
            {
                Debug.LogWarning("TbLevel 表为空或不可用");
            }

            BuildGraphicDataMap();
        }

        private void BuildGraphicDataMap()
        {
            GraphicDataMap = new Dictionary<string, TextGraphicData>();
            if (_graphicDataAsset == null || _graphicDataAsset.TextGraphicDataList == null) return;

            foreach (var gd in _graphicDataAsset.TextGraphicDataList)
            {
                if (gd != null && !string.IsNullOrEmpty(gd.character))
                {
                    GraphicDataMap[gd.character] = gd;
                }
            }
            Debug.Log($"加载了 {GraphicDataMap.Count} 个字形数据");
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

        /// <summary>获取字符的图形数据</summary>
        public TextGraphicData GetGraphicData(string character)
        {
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