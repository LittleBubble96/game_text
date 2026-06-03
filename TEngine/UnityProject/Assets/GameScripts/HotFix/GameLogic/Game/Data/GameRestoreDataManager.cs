using System.IO;
using GameLogic.GamePlay.CorePlay;
using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// 游戏存档管理器 —— JSON 本地存储
    /// 保存时机：每次通关后 / OnApplicationPause(pause=true)
    /// </summary>
    public class GameRestoreDataManager
    {
        private const string SaveFileName = "coreplay_save.json";

        public CorePlayRestore CorePlayRestoreData { get; private set; }

        public GameRestoreDataManager()
        {
            CorePlayRestoreData = new CorePlayRestore();
        }

        /// <summary>获取存档文件路径</summary>
        private string GetSaveFilePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }

        /// <summary>保存数据到本地 JSON 文件</summary>
        public void Save()
        {
            if (CorePlayRestoreData?.SaveData == null) return;

            try
            {
                string json = JsonUtility.ToJson(CorePlayRestoreData.SaveData, true);
                string filePath = GetSaveFilePath();
                File.WriteAllText(filePath, json);
                Debug.Log($"[RestoreDataManager] 存档已保存: {filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RestoreDataManager] 保存失败: {e.Message}");
            }
        }

        /// <summary>从本地 JSON 文件加载数据</summary>
        public void Load()
        {
            string filePath = GetSaveFilePath();
            if (!File.Exists(filePath))
            {
                Debug.Log($"[RestoreDataManager] 未找到存档文件，使用新数据: {filePath}");
                CorePlayRestoreData.InitOrResetData();
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                CorePlaySaveData data = JsonUtility.FromJson<CorePlaySaveData>(json);
                CorePlayRestoreData.LoadFromData(data);
                Debug.Log($"[RestoreDataManager] 存档已加载: 当前关卡 {data?.currentLevelIndex}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RestoreDataManager] 加载失败: {e.Message}");
                CorePlayRestoreData.InitOrResetData();
            }
        }

        /// <summary>删除存档文件</summary>
        public void DeleteSaveFile()
        {
            string filePath = GetSaveFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log("[RestoreDataManager] 存档已删除");
            }
            CorePlayRestoreData.InitOrResetData();
        }
    }
}