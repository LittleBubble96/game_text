using System;
using System.Collections.Generic;
using GameLogic.Data;

namespace GameLogic.GamePlay.CorePlay
{
    /// <summary>
    /// CorePlay 的存档/恢复数据 —— 只存当前关卡的进度
    /// </summary>
    [Serializable]
    public class CorePlaySaveData
    {
        public int currentLevelIndex;

        /// <summary>当前关卡已找到的答案索引</summary>
        public List<int> foundAnswerIndices = new List<int>();

        /// <summary>
        /// 当前关卡的关卡数据快照 —— 缓存玩家开始该关卡时的关卡配置，
        /// 确保即使后续配置更新，玩家的进度仍然基于当时的关卡数据。
        /// 为 null 表示尚未缓存（首次进入，应从配置加载）。
        /// </summary>
        public TextLevelData cachedLevelData;
    }

    public class CorePlayRestore : IRestoreData
    {
        public CorePlaySaveData SaveData { get; private set; }

        public void InitOrResetData()
        {
            SaveData = new CorePlaySaveData { currentLevelIndex = 0 };
        }

        /// <summary>从存档数据恢复</summary>
        public void LoadFromData(CorePlaySaveData data)
        {
            SaveData = data ?? new CorePlaySaveData { currentLevelIndex = 0 };
        }

        /// <summary>更新当前关卡索引</summary>
        public void SetCurrentLevel(int levelIndex)
        {
            if (SaveData == null) InitOrResetData();
            SaveData.currentLevelIndex = levelIndex;
        }

        /// <summary>保存当前关卡进度（答案 + 关卡数据快照）</summary>
        public void SaveCurrentProgress(int levelIndex, List<int> foundAnswerIndices, TextLevelData levelDataCache)
        {
            if (SaveData == null) InitOrResetData();
            SaveData.currentLevelIndex = levelIndex;
            SaveData.foundAnswerIndices = new List<int>(foundAnswerIndices);
            SaveData.cachedLevelData = levelDataCache;
        }

        /// <summary>获取当前关卡已找到的答案索引</summary>
        public List<int> GetFoundAnswers()
        {
            if (SaveData == null) return new List<int>();
            return SaveData.foundAnswerIndices ?? new List<int>();
        }

        /// <summary>获取当前关卡的缓存关卡数据快照（可能为 null）</summary>
        public TextLevelData GetCachedLevelData()
        {
            return SaveData?.cachedLevelData;
        }
    }
}