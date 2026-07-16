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
    }
}