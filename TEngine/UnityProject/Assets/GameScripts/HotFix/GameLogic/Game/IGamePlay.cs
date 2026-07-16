using System;
using GameLogic.Data;

namespace GameLogic.GamePlay
{
    /// <summary>
    /// 通用玩法接口 —— 方便后续扩展新玩法
    /// </summary>
    public interface IGamePlay
    {
        void StartGame();
        void EndGame();
        bool IsGameOver();

        /// <summary>当前关卡ID</summary>
        int CurrentLevelId { get; }
        
        /// <summary>当前关卡数据</summary>
        TextLevelData CurrentLevelData { get; }

        /// <summary>切换/选中笔画</summary>
        void ToggleStroke(int strokeIndex);

        /// <summary>提交当前选中的笔画作为答案</summary>
        void SubmitAnswer();

        /// <summary>加载指定关卡</summary>
        void LoadLevel(int levelId);
        
        /// <summary>关卡通关事件</summary>
        event Action<int> OnLevelCompleted;
    }
}