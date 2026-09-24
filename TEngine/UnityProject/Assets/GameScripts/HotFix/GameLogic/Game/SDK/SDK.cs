namespace GameLogic
{
    public static class SDK
    {
        private static ISdk _sdk;

        public static void InitSdk()
        {
#if UNITY_EDITOR
            _sdk = new EditorSDK();
#elif UNITY_WEBGL
            _sdk = new WXSDK();
#endif
        }
        
        public static IRewardedVideoAd CreateRewardedVideoAd(string adId, System.Action onLoaded,
            System.Action<int,string> onError, System.Action<bool> onClosed)
        {
            if (_sdk == null) throw new System.InvalidOperationException("SDK 未初始化");
            return _sdk.CreateRewardedVideoAd(adId, onLoaded, onError, onClosed);
        }

        public static string GetOpenId()
        {
            return _sdk?.GetOpenId();
        }

        public static void ShareAppMessage(string title)
        {
            _sdk?.ShareAppMessage(title);
        }

        public static void CreateGameClubButton()
        {
            _sdk?.CreateGameClubButton();
        }
        
        public static void OpenGameClub()
        {
            _sdk?.OpenGameClub();
        }

        public static void ReportGameStart()
        {
            _sdk?.ReportGameStart();
        }

        /// <summary>返回是否已调用平台接口，不代表服务端接收成功。</summary>
        public static bool ReportEvent(string eventId, System.Collections.Generic.Dictionary<string, string> data)
        {
            if (_sdk == null) return false;
            _sdk.ReportEvent(eventId, data);
            return true;
        }
    }
}
