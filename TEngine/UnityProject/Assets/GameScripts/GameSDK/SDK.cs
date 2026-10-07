using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSDK
{
    /// <summary>Procedure 与 HotFix 共用的常驻 SDK 入口，所有 BI 经登录门禁发送。</summary>
    public static class SDK
    {
        private static ISdk _sdk;
        private static SdkSession _session = NewSession();
        private static SdkSession NewSession() => new SdkSession(
            () => Time.realtimeSinceStartupAsDouble, message => Debug.LogWarning(message));
        public static bool IsLoggedIn => _session.IsLoggedIn;
        public static SdkLoginState LoginState => _session.LoginState;
        public static string OpenId => _session.OpenId;
        public static int PendingBiCount => _session.PendingCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _sdk = null;
            _session = NewSession();
        }

        public static void InitializeAndLogin()
        {
            if (_sdk != null) return;
#if UNITY_EDITOR
            _sdk = new EditorSDK();
#elif UNITY_WEBGL
            _sdk = new WXSDK();
#else
            Debug.LogWarning("[SDK] Unsupported platform; BI remains buffered.");
            return;
#endif
            var runner = new GameObject("SDKRuntime");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<SDKRuntime>();
            _session.Initialize(_sdk);
        }

        internal static void Tick() => _session.Tick();
        public static void RetryLogin() => _session.RetryLogin();
        public static void RetryPendingEvents() => _session.RetryPendingEvents();
        public static string GetOpenId() => OpenId;
        public static IRewardedVideoAd CreateRewardedVideoAd(string adId, Action onLoaded,
            Action<int, string> onError, Action<bool> onClosed)
        {
            if (_sdk == null) throw new InvalidOperationException("SDK 未初始化");
            return _sdk.CreateRewardedVideoAd(adId, onLoaded, onError, onClosed);
        }
        public static void ShareAppMessage(string title) => _sdk?.ShareAppMessage(title);
        public static void CreateGameClubButton() => _sdk?.CreateGameClubButton();
        public static void OpenGameClub() => _sdk?.OpenGameClub();
        public static void ReportGameStart() => _session.ReportGameStart();

        /// <summary>true 表示本地已接收（可能等待登录或重试），不代表远端送达。</summary>
        public static bool ReportEvent(string eventId, Dictionary<string, string> data = null)
            => _session.ReportEvent(eventId, data);
    }
}
