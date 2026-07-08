namespace GameLogic
{
    /// <summary>
    /// UITop 更新数据结构 —— 整体传递，避免单字段更新
    /// </summary>
    public struct UITopData
    {
        /// <summary>是否显示金币区域</summary>
        public bool ShowCoin;

        /// <summary>是否显示返回按钮</summary>
        public bool ShowBack;

        public UITopData(bool showCoin, bool showBack)
        {
            ShowCoin = showCoin;
            ShowBack = showBack;
        }
    }
}
