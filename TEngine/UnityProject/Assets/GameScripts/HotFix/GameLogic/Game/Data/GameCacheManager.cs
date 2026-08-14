using System.IO;
using GameLogic.GamePlay.CorePlay;
using UnityEngine;
using WeChatWASM;

namespace GameLogic.Data
{
    /// <summary>
    /// 通用游戏缓存管理器 —— JSON 本地存储
    /// 统一管理关卡缓存、设置缓存、道具缓存。
    ///
    /// 使用示例：
    ///   var cache = new GameCacheManager();
    ///   cache.Load();
    ///   cache.GameSettings.language = "zh";                   // 改设置
    ///   cache.CorePlayRestore.SetCurrentLevel(3);             // 改关卡
    ///   cache.CacheData.gamePropData.tipCount = 5;            // 改道具
    ///   cache.Save();   // 一次性保存全部
    /// </summary>
    public class GameCacheManager
    {
        /// <summary>缓存数据容器</summary>
        public GameCacheData CacheData { get; private set; }

        /// <summary>关卡缓存操作</summary>
        public CorePlayRestore CorePlayRestore { get; private set; }
        
        private IReadCacheFileSystem _readCacheFileSystem;

        public GameCacheManager()
        {
            CacheData = new GameCacheData();
            CorePlayRestore = new CorePlayRestore();
            _readCacheFileSystem = GenerateFileSystem();
        }

        private IReadCacheFileSystem GenerateFileSystem()
        {
#if UNITY_EDITOR
            return new WindowReadCacheFileSystem();
#endif
            return new WeChatReadCacheFileSystem();
        }
        

        /// <summary>
        /// 保存全部缓存到本地 JSON 文件。
        /// 调用前确保各子缓存已填充到 CacheData 中。
        /// </summary>
        public void Save()
        {
            // 将 CorePlayRestore 数据同步到 CacheData
            CacheData.corePlaySaveData = CorePlayRestore.SaveData;

            try
            {
                string json = JsonUtility.ToJson(CacheData, true);
                _readCacheFileSystem?.WriteCache(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameCache] 保存失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从本地 JSON 文件加载全部缓存。
        /// 如果文件不存在或加载失败，自动初始化所有子缓存。
        /// </summary>
        public void Load()
        {
            string cacheJson = _readCacheFileSystem?.ReadCache();
            if (string.IsNullOrEmpty(cacheJson))
            {
                Debug.Log("[GameCache] 未找到缓存文件，初始化新数据");
                InitAll();
                return;
            }
            
            try
            {
                GameCacheData data = JsonUtility.FromJson<GameCacheData>(cacheJson);
                if (data != null)
                {
                    CacheData = data;
                    CacheData.InitAll(); // 确保所有子字段非 null
                }
                else
                {
                    InitAll();
                }

                // JsonUtility 可能将 JSON null 反序列化为默认空实例，需做有效性校验
                ValidateCachedLevelData();

                // 将关卡数据同步到 CorePlayRestore
                CorePlayRestore.LoadFromData(CacheData.corePlaySaveData);
                Debug.Log($"[GameCache] 缓存已加载: 关卡ID={CacheData.corePlaySaveData?.currentLevelId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameCache] 加载失败: {e.Message}");
                InitAll();
            }
        }

        /// <summary>删除缓存文件并重置所有数据</summary>
        public void DeleteAll()
        {
            _readCacheFileSystem?.DeleteAll();
            InitAll();
        }

        /// <summary>
        /// 通关推进：将存档推进到 completedLevelId+1（清空答案进度与关卡快照）并落盘。
        /// 仅在关卡通关时调用一次，集中处理“推进存档”，避免各处分散打补丁。
        /// 通关最后一关时存档会推进到 MaxLevelId+1（超过最大关），进入游戏时
        /// 据此判断“已全通关”弹提示；不持久化通关标志，后续更新新增关卡后可继续。
        /// </summary>
        public void AdvanceToNextLevel(int completedLevelId)
        {
            CorePlayRestore.AdvanceToNextLevel(completedLevelId + 1);
            Save();
        }

        /// <summary>初始化所有子缓存</summary>
        private void InitAll()
        {
            CacheData = new GameCacheData();
            CacheData.InitAll();
            CorePlayRestore.InitOrResetData();
        }

        /// <summary>
        /// 校验缓存中的关卡数据快照有效性。
        /// JsonUtility 反序列化时可能将 JSON null 创建为默认空实例
        /// （baseCharacter 为空），需做二次校验并修正为 null。
        /// </summary>
        private void ValidateCachedLevelData()
        {
            var cached = CacheData?.corePlaySaveData?.cachedLevelData;
            if (cached != null && !cached.IsValid())
            {
                Debug.LogWarning("[GameCache] 检测到无效的缓存关卡数据，已重置为 null。");
                CacheData.corePlaySaveData.cachedLevelData = null;
            }
        }
    }
}
