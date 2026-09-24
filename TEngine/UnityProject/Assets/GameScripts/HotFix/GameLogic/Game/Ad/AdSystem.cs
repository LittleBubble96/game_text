using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
    public enum RewardedAdState { Idle, Loading, Ready, Showing, Failed }

    /// <summary>广告管理入口。每个 adId 一个独立执行流；广告位之间不互斥，UIAd 负责拦截玩家点击。</summary>
    public static class AdSystem
    {
        public const float LoadingTimeoutSeconds = 15f;
        public static event Action<string> StateChanged;
        private static readonly Dictionary<string, RewardedAdFlow> RewardedAds = new Dictionary<string, RewardedAdFlow>();
        private static readonly HashSet<int> OverlayRequests = new HashSet<int>();
        public const float CloseCallbackTimeoutSeconds = 60f;
        private static int _requestId;
        public static bool HasOverlay => OverlayRequests.Count > 0;

        private static RewardedAdFlow GetFlow(string adId)
        {
            adId = adId ?? string.Empty;
            if (!RewardedAds.TryGetValue(adId, out var flow))
            {
                flow = new RewardedAdFlow(adId);
                RewardedAds.Add(adId, flow);
            }
            return flow;
        }
        public static RewardedAdState GetState(string adId) =>
            RewardedAds.TryGetValue(adId ?? string.Empty, out var flow) ? flow.State : RewardedAdState.Idle;
        public static bool IsAdAvailable(string adId) =>
            RewardedAds.TryGetValue(adId ?? string.Empty, out var flow) && flow.IsAdAvailable;
        public static bool IsLoading(string adId) => GetState(adId) == RewardedAdState.Loading;
        public static bool IsBusy(string adId) =>
            RewardedAds.TryGetValue(adId ?? string.Empty, out var flow) && flow.IsBusy;
        public static void Preload(string adId) => GetFlow(adId).Preload();
        public static void EnterPage(params string[] adIds)
        {
            foreach (var id in new HashSet<string>(adIds)) GetFlow(id).EnterPage();
        }
        public static void ExitPage(params string[] adIds)
        {
            foreach (var id in new HashSet<string>(adIds))
                if (RewardedAds.TryGetValue(id ?? string.Empty, out var flow)) flow.ExitPage();
        }
        public static UniTask<bool> ShowRewardedAdAsync(string adId) => GetFlow(adId).ShowRewardedAdAsync();
        public static void Release(string adId)
        {
            adId = adId ?? string.Empty;
            if (!RewardedAds.TryGetValue(adId, out var flow)) return;
            RewardedAds.Remove(adId);
            flow.Dispose();
        }
        public static bool ShouldShowOverlay(int requestId) => OverlayRequests.Contains(requestId);
        internal static int NextRequestId() => ++_requestId;
        internal static void OpenOverlay(int requestId) => OverlayRequests.Add(requestId);
        internal static void CloseOverlay(int requestId)
        {
            OverlayRequests.Remove(requestId);
            if (!HasOverlay) GameModule.UI.CloseUI<UIAd>();
        }
        internal static void NotifyStateChanged(string adId) => StateChanged?.Invoke(adId);
    }
}
