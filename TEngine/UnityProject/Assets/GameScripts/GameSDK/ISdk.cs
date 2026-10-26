namespace GameSDK
{
    public interface ISdk
    {
        void Login(System.Action<string> onSuccess, System.Action<string> onFailure);

        IRewardedVideoAd CreateRewardedVideoAd(string adId, System.Action onLoaded,
            System.Action<int ,string> onError, System.Action<bool> onClosed);

        string GetOpenId();

        void ShareAppMessage(string title);

        void CreateGameClubButton();

        void OpenGameClub();

        void LoadRecommend();
        void ShowRecommend(System.Func<bool> canShow);

        void ReportGameStart();

        void UploadCompletedLevel(int levelId);

        void ReportLevel(LevelReport report);

        void ReportEvent(string eventId, System.Collections.Generic.Dictionary<string, string> data);
    }
}
