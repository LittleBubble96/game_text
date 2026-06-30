using System;
using System.Collections.Generic;
using GameLogic.GamePlay.CorePlay;

namespace GameLogic.Data
{
    /// <summary>
    /// 自定义缓存项（JsonUtility 不支持 Dictionary，用 List 模拟）
    /// </summary>
    [Serializable]
    public class CustomCacheEntry
    {
        public string key;
        public string jsonData;     // 自定义对象序列化后的 JSON 字符串
    }

    /// <summary>
    /// 顶层缓存数据容器 —— 包含所有子缓存模块。
    /// 新增缓存类型时，在此处添加字段即可。
    /// </summary>
    [Serializable]
    public class GameCacheData
    {
        // ===== 关卡缓存 =====
        public CorePlaySaveData corePlaySaveData;

        // ===== 设置缓存 =====
        public GameSettingsData gameSettingsData;

        // ===== 自定义类缓存（键值对，值存 JSON 字符串） =====
        public List<CustomCacheEntry> customDataList = new List<CustomCacheEntry>();

        /// <summary>初始化所有子缓存（首次使用时调用）</summary>
        public void InitAll()
        {
            if (corePlaySaveData == null)
                corePlaySaveData = new CorePlaySaveData();

            if (gameSettingsData == null)
                gameSettingsData = new GameSettingsData();

            if (customDataList == null)
                customDataList = new List<CustomCacheEntry>();
        }

        // ===== 自定义缓存操作 =====

        /// <summary>存入自定义数据（自动序列化为 JSON）</summary>
        public void SetCustomData<T>(string key, T data)
        {
            if (customDataList == null) customDataList = new List<CustomCacheEntry>();
            string json = UnityEngine.JsonUtility.ToJson(data);

            var entry = customDataList.Find(e => e.key == key);
            if (entry != null)
            {
                entry.jsonData = json;
            }
            else
            {
                customDataList.Add(new CustomCacheEntry { key = key, jsonData = json });
            }
        }

        /// <summary>读取自定义数据（自动反序列化）</summary>
        public T GetCustomData<T>(string key)
        {
            if (customDataList == null) return default;
            var entry = customDataList.Find(e => e.key == key);
            if (entry == null || string.IsNullOrEmpty(entry.jsonData)) return default;
            return UnityEngine.JsonUtility.FromJson<T>(entry.jsonData);
        }

        /// <summary>是否存在指定 key 的自定义数据</summary>
        public bool HasCustomData(string key)
        {
            if (customDataList == null) return false;
            return customDataList.Exists(e => e.key == key);
        }

        /// <summary>删除指定 key 的自定义数据</summary>
        public void RemoveCustomData(string key)
        {
            if (customDataList == null) return;
            customDataList.RemoveAll(e => e.key == key);
        }
    }
}
