using System.Collections.Generic;

namespace GameLogic
{
    public static class EventDefine
    {
        //转hash
        public static int Event_AnswerSubmitted = "Event_AnswerSubmitted".GetHashCode();
        public static int Event_LevelCompleted = "Event_LevelCompleted".GetHashCode();

        /// <summary>UITop 更新事件，携带 UITopData</summary>
        public static int Event_UITopUpdate = "Event_UITopUpdate".GetHashCode();

        /// <summary>UITop 金币数量更新事件，携带 int</summary>
        public static int Event_UITopCoinUpdate = "Event_UITopCoinUpdate".GetHashCode();

        /// <summary>Slot 布局数据更新事件，携带 SlotViewLayoutData</summary>
        public static int Event_SlotLayoutUpdate = "Event_SlotLayoutUpdate".GetHashCode();

        /// <summary>Character 渲染区布局数据更新事件，携带 ContentViewLayoutData（可用宽高，世界坐标）</summary>
        public static int Event_CharacterLayoutUpdate = "Event_CharacterLayoutUpdate".GetHashCode();

        /// <summary>道具提示高亮笔画事件，携带 List&lt;int&gt;（需要高亮的笔画索引列表）</summary>
        public static int Event_PropTipHighlight = "Event_PropTipHighlight".GetHashCode();

        /// <summary>清除提示高亮事件</summary>
        public static int Event_PropTipClearHighlight = "Event_PropTipClearHighlight".GetHashCode();

        /// <summary>道具数量变化事件，携带 PropType 和 int（新数量）</summary>
        public static int Event_PropCountChanged = "Event_PropCountChanged".GetHashCode();

        /// <summary>重置道具使用完成事件（清空答案并重载本关后发送，UI 据此刷新进度文字）</summary>
        public static int Event_PropResetDone = "Event_PropResetDone".GetHashCode();

        /// <summary>UITop 金币增加动画事件，携带 int（增加数量），0.6s 内完成递增</summary>
        public static int Event_UITopCoinAddAnim = "Event_UITopCoinAddAnim".GetHashCode();
        
        /// <summary>新的一天事件</summary>
        public static int Event_NewDay = "Event_NewDay".GetHashCode();
    }
}