using TEngine;

namespace GameSDK
{
    public class EditorSDK : ISdk
    {
        public void Login(System.Action<string> onSuccess, System.Action<string> onFailure) => onSuccess("Editor");

        public IRewardedVideoAd CreateRewardedVideoAd(string adId, System.Action onLoaded,
            System.Action<int ,string> onError, System.Action<bool> onClosed)
        {
            return new EditorRewardedVideoAd(onLoaded, onClosed);
        }

        private sealed class EditorRewardedVideoAd : IRewardedVideoAd
        {
            private readonly System.Action _loaded;
            private readonly System.Action<bool> _closed;
            private bool _disposed;
            public EditorRewardedVideoAd(System.Action loaded, System.Action<bool> closed)
            { _loaded = loaded; _closed = closed; }
            public void Load(System.Action onLoaded, System.Action<string> onFailed)
            {
                if (_disposed) { onFailed?.Invoke("广告实例已释放"); return; }
                _loaded?.Invoke();
                onLoaded?.Invoke();
            }
            public void Show(System.Action onShown, System.Action<string> onFailed)
            {
                if (_disposed) { onFailed?.Invoke("广告实例已释放"); return; }
                onShown?.Invoke();
                _closed?.Invoke(true); // 编辑器直接模拟完整观看。
            }
            public void Dispose() { _disposed = true; }
        }

        public string GetOpenId()
        {
            return "Editor";
        }

        public void ShareAppMessage(string title)
        {
            Log.Info($"[Share] 分享 {title}");
        }

        public void CreateGameClubButton()
        {
            Log.Info($"[GameCenter] 打开游戏圈");
        }

        public void OpenGameClub()
        {
            Log.Info($"[GameCenter] OpenGameClub 打开游戏圈");
        }

        public void ReportGameStart()
        {
            Log.Info($"[ReportGameStart] 上报");
        }

        private bool _recommendShown;
        public void UploadCompletedLevel(int levelId)
        {
            if (levelId > 0) Log.Info($"[Leaderboard] ToLevel={levelId}（编辑器模拟）");
        }

        public void LoadRecommend() => Log.Info("[Recommend] 预加载评价与推荐组件（编辑器模拟）");
        public void ShowRecommend(System.Func<bool> canShow)
        {
            if (_recommendShown || canShow == null || !canShow()) return;
            _recommendShown = true;
            Log.Info("[Recommend] 第10关结算展示评价与推荐组件（编辑器模拟）");
        }

        public void ReportEvent(string eventId, System.Collections.Generic.Dictionary<string, string> data)
        {
            var fields = new System.Collections.Generic.List<string>();
            if (data != null)
            {
                foreach (var field in data)
                    fields.Add($"{field.Key}={field.Value}");
            }
            Log.Info($"[BI] {eventId} | {string.Join(", ", fields)}");
        }

        public void ReportLevel(LevelReport report) => Log.Info("[LevelReport] " + report.ToJson());
    }
}
