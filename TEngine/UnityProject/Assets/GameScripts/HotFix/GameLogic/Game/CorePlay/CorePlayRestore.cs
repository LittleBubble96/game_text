using System;
using System.Collections.Generic;
using GameLogic.Data;

namespace GameLogic.GamePlay.CorePlay
{
    /// <summary>
    /// CorePlay 的存档/恢复数据
    /// </summary>
    [Serializable]
    public class CorePlaySaveData
    {
        public int currentLevelIndex;
        public List<LevelSaveData> levelProgresses = new List<LevelSaveData>();
    }

    [Serializable]
    public class LevelSaveData
    {
        public int levelIndex;
        public List<int> foundAnswerIndices = new List<int>();
    }

    public class CorePlayRestore : IRestoreData
    {
        public CorePlaySaveData SaveData { get; private set; }

        public void InitOrResetData()
        {
            SaveData = new CorePlaySaveData
            {
                currentLevelIndex = 0,
                levelProgresses = new List<LevelSaveData>()
            };
        }

        /// <summary>从存档数据恢复</summary>
        public void LoadFromData(CorePlaySaveData data)
        {
            SaveData = data ?? new CorePlaySaveData
            {
                currentLevelIndex = 0,
                levelProgresses = new List<LevelSaveData>()
            };
        }

        /// <summary>更新当前关卡索引</summary>
        public void SetCurrentLevel(int levelIndex)
        {
            if (SaveData == null) InitOrResetData();
            SaveData.currentLevelIndex = levelIndex;
        }

        /// <summary>记录某个关卡已找到的答案</summary>
        public void SetFoundAnswers(int levelIndex, List<int> foundAnswerIndices)
        {
            if (SaveData == null) InitOrResetData();
            var existing = SaveData.levelProgresses.Find(p => p.levelIndex == levelIndex);
            if (existing != null)
            {
                existing.foundAnswerIndices = new List<int>(foundAnswerIndices);
            }
            else
            {
                SaveData.levelProgresses.Add(new LevelSaveData
                {
                    levelIndex = levelIndex,
                    foundAnswerIndices = new List<int>(foundAnswerIndices)
                });
            }
        }

        /// <summary>获取某个关卡已找到的答案索引</summary>
        public List<int> GetFoundAnswers(int levelIndex)
        {
            if (SaveData == null) return new List<int>();
            var existing = SaveData.levelProgresses.Find(p => p.levelIndex == levelIndex);
            return existing != null ? new List<int>(existing.foundAnswerIndices) : new List<int>();
        }
    }
}