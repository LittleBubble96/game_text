namespace GameLogic
{
    public interface ISdk
    {
        IRewardedVideoAd CreateRewardedVideoAd(string adId, System.Action onLoaded,
            System.Action<int ,string> onError, System.Action<bool> onClosed);

        string GetOpenId();

        void ShareAppMessage(string title);

        void CreateGameClubButton();

        void OpenGameClub();

        void ReportGameStart();

        void ReportEvent(string eventId, System.Collections.Generic.Dictionary<string, string> data);
    }
}
