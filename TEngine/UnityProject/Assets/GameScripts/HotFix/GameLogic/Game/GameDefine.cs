namespace GameLogic
{
    public static class GameDefine
    {
        public const int ShareCountByDay = 5;
        //分享一次获取金币为30
        public const int ShareCoinByDay = 30;
        //新一天获取金币为30
        public const int NewDayCoinByDay = 30;

        //道具用金币替代使用时的消耗量（数量不足、金币足够时点击即扣此数量）
        public const int PropTipCoinCost = 100;   //提示道具
        public const int PropResetCoinCost = 50;  //重置道具
    }
}