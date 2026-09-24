using System;
using System.Collections.Generic;
using WeChatWASM;

namespace GameLogic
{
    public class WXSDK : ISdk
    {
        public IRewardedVideoAd CreateRewardedVideoAd(string adId, Action onLoaded,
            Action<int ,string> onError, Action<bool> onClosed)
        {
            if (string.IsNullOrWhiteSpace(adId)) throw new ArgumentException("微信广告位 ID 不能为空", nameof(adId));
            return new WeChatRewardedVideoAd(adId, onLoaded, onError, onClosed);
        }

        private sealed class WeChatRewardedVideoAd : IRewardedVideoAd
        {
            private readonly WXRewardedVideoAd _native;
            private readonly string _adId;
            private void Trace(string message) => TEngine.Log.Info($"[AdSDK][{_adId}] {message}");
            private readonly Action<WXADLoadResponse> _loaded;
            private readonly Action<WXADErrorResponse> _error;
            private readonly Action<WXRewardedVideoAdOnCloseResponse> _closed;
            private bool _disposed;
            public WeChatRewardedVideoAd(string adId, Action onLoaded, Action<int ,string> onError, Action<bool> onClosed)
            {
                _adId = adId;
                Trace("create multiton=true");
                _native = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam { adUnitId = adId, multiton = true });
                _loaded = _ => { Trace($"onLoad disposed={_disposed}"); if (!_disposed) onLoaded?.Invoke(); };
                _error = e => { Trace($"onError disposed={_disposed} code={e.errCode} message={e.errMsg}"); if (!_disposed) onError?.Invoke(e.errCode , e.errMsg); };
                _closed = result => { Trace($"onClose disposed={_disposed} isEnded={result?.isEnded} legacyNull={result == null}"); if (!_disposed) onClosed?.Invoke(result == null || result.isEnded); };
                _native.OnLoad(_loaded);
                _native.OnError(_error);
                _native.OnClose(_closed);
            }
            public void Load(Action onLoaded, Action<string> onFailed)
            {
                if (_disposed) { onFailed?.Invoke("广告实例已释放"); return; }
                Trace("load begin");
                _native.Load(_ => { Trace($"load success disposed={_disposed}"); if (!_disposed) onLoaded?.Invoke(); },
                    e => { Trace($"load fail code={e.errCode} message={e.errMsg}"); if (!_disposed) onFailed?.Invoke($"{e.errCode}: {e.errMsg}"); });
            }
            public void Show(Action onShown, Action<string> onFailed)
            {
                if (_disposed) { onFailed?.Invoke("广告实例已释放"); return; }
                Trace("show begin");
                _native.Show(_ => { Trace($"show success disposed={_disposed}"); if (!_disposed) onShown?.Invoke(); },
                    e => { Trace($"show fail {e.errMsg}"); if (!_disposed) onFailed?.Invoke(e.errMsg); });
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Trace("dispose: offLoad/offError/offClose/destroy");
                try
                {
                    _native.OffLoad(_loaded);
                    _native.OffError(_error);
                    _native.OffClose(_closed);
                }
                finally { _native.Destroy(); }
            }
        }

        private const string PlayerPrefsOpenIdKey = "WX_openid";

        private const string GameCenterOpenLink =
            "-SSEykJvFV3pORt5kTNpS-gRdTx2iixOqRRPWz1BBnl69Ttni0ZLetaZXdSifeYATwC6iKRakfy-6prIB-gkjUfjCAtcHiTCxM1bcnJbYR0-ySyYid8eeaXsY-nGOaapr01q8G1TZQMpj0HxwhGL1mQuWkh3AVgpBr3iLWw6rvKmYoovB9BGVAoipFp0WhSRL2E10raoOsWP_BlylMxH3ezmOzqgTGZguMmYcRV-LVPuHlxg9GZ6ZHK6w8Y12MNsWKRoFruMCFyeKCA4uKTqSZUtSDaFPIvXxZfPJ7t-YtwNVoEgolltTSJhZwLbMzoO-dzYquOABWuOgoo6A6YU-A";
        
        public string GetOpenId()
        {
            return PlayerPrefs.GetString(PlayerPrefsOpenIdKey , "");
        }

        public void ShareAppMessage(string title)
        {
            WX.ShareAppMessage(new ShareAppMessageOption {
                title=title,
                query=$"inviter={GetOpenId()}&sid={Guid.NewGuid():N}"
            });
        }

        public void CreateGameClubButton()
        {
            WXCreateGameClubButtonParam param = new WXCreateGameClubButtonParam();
            GameClubButtonStyle clubButtonStyle = new GameClubButtonStyle();
            clubButtonStyle.left = 10;
            clubButtonStyle.top = 76;
            clubButtonStyle.width = 40;
            clubButtonStyle.height = 40;
            param.style = clubButtonStyle;
            param.type = GameClubButtonType.text;
            WX.CreateGameClubButton(param);
        }

        public void OpenGameClub()
        {
            WXPageManager pageManager = WX.CreatePageManager();
            pageManager.Load(new LoadOption()
            {
                openlink = GameCenterOpenLink,
                success = (result) =>
                {
                    pageManager.Show(new ShowOption()
                    {
                        openlink = GameCenterOpenLink,
                    });
                },
            });
        }

        public void ReportGameStart()
        {
            WX.ReportGameStart();
        }

        public void ReportEvent(string eventId , Dictionary<string,string> data)
        {
            WX.ReportEvent(eventId,data);
        }
    }
}
