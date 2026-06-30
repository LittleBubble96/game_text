using System.IO;
using GameLogic.GamePlay.CorePlay;
using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// 通用游戏缓存管理器 —— JSON 本地存储
    /// 统一管理关卡缓存、设置缓存、自定义类缓存。
    ///
    /// 使用示例：
    ///   var cache = new GameCacheManager();
    ///   cache.Load();
    ///   cache.GameSettings.masterVolume = 0.5f;               // 改设置
    ///   cache.CorePlayRestore.SetCurrentLevel(3);             // 改关卡
    ///   cache.CacheData.SetCustomData("hero", myHeroData);    // 存自定义类
    ///   var hero = cache.CacheData.GetCustomData<HeroData>("hero"); // 读自定义类
    ///   cache.Save();   // 一次性保存全部
    /// </summary>
    public class GameCacheManager
    {
        private const string SaveFileName = "game_cache.json";

        /// <summary>缓存数据容器</summary>
        public GameCacheData CacheData { get; private set; }

        /// <summary>关卡缓存操作（兼容旧接口）</summary>
        public CorePlayRestore CorePlayRestore { get; private set; }

        /// <summary>设置缓存（直接访问字段）</summary>
        public GameSettingsData GameSettings => CacheData?.gameSettingsData;

        public GameCacheManager()
        {
            CacheData = new GameCacheData();
            CorePlayRestore = new CorePlayRestore();
        }

        /// <summary>获取存档文件路径</summary>
        private string GetSaveFilePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
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
                string filePath = GetSaveFilePath();
                File.WriteAllText(filePath, json);
                Debug.Log($"[GameCache] 缓存已保存: {filePath}");
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
            string filePath = GetSaveFilePath();
            if (!File.Exists(filePath))
            {
                Debug.Log($"[GameCache] 未找到缓存文件，初始化新数据: {filePath}");
                InitAll();
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                GameCacheData data = JsonUtility.FromJson<GameCacheData>(json);
                if (data != null)
                {
                    CacheData = data;
                    CacheData.InitAll(); // 确保所有子字段非 null
                }
                else
                {
                    InitAll();
                }

                // 将关卡数据同步到 CorePlayRestore
                CorePlayRestore.LoadFromData(CacheData.corePlaySaveData);
                Debug.Log($"[GameCache] 缓存已加载: 关卡={CacheData.corePlaySaveData?.currentLevelIndex}");
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
            string filePath = GetSaveFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log("[GameCache] 缓存文件已删除");
            }
            InitAll();
        }

        /// <summary>初始化所有子缓存</summary>
        private void InitAll()
        {
            CacheData = new GameCacheData();
            CacheData.InitAll();
            CorePlayRestore.InitOrResetData();
        }
    }
}
