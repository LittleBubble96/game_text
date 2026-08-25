namespace GameLogic.Data
{
    [System.Serializable]
    public class GameCommonData
    {
        public int lastDayTime = -1;
        
        //每日领取
        public bool isClaimEveryDayCoin = false;
        
        //每日分享次数
        public int shareCountByGetCoin = GameDefine.ShareCountByDay;
    }
}