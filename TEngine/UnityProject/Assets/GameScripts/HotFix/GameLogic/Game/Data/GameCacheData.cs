using System;
using GameLogic.GamePlay.CorePlay;

namespace GameLogic.Data
{
    /// <summary>
    /// 道具数据（直接参与存档序列化）
    /// </summary>
    [Serializable]
    public class GamePropData
    {
        public int coinCount;
        public int tipCount;
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

        // ===== 道具缓存 =====
        public GamePropData gamePropData;
        
        // ===== 公共数据 =====
        public GameCommonData commonData;

        /// <summary>初始化所有子缓存（首次使用时调用）</summary>
        public void InitAll()
        {
            if (corePlaySaveData == null)
                corePlaySaveData = new CorePlaySaveData();

            if (gameSettingsData == null)
                gameSettingsData = new GameSettingsData();

            if (gamePropData == null)
                gamePropData = new GamePropData { tipCount = 3, coinCount = 0 };

            if (commonData == null)
            {
                commonData = new GameCommonData();
            }
        }
    }
}
