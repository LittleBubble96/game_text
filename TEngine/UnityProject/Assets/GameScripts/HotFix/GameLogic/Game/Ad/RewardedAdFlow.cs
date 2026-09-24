using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Cysharp.Threading.Tasks;
using TEngine;

namespace GameLogic
{
    /// <summary>一个广告位一个执行流；实例版本阻止旧实例回调影响新请求。</summary>
    internal sealed class RewardedAdFlow
    {
        private readonly string _adId;
        private bool _disposed, _pageActive, _loadPending;
        private IRewardedVideoAd _ad;
        // 每次销毁/重建递增。异步闭包捕获版本，迟到回调必须先校验。
        private int _instanceVersion, _loadVersion, _retryCount;
        private string _loadId;
        private double _loadStarted;
        private Request _request;
        public RewardedAdFlow(string adId) { _adId = adId; }
        public RewardedAdState State { get; private set; }
        public bool IsAdAvailable => !_disposed && State == RewardedAdState.Ready && _request == null;
        public bool IsLoading => State == RewardedAdState.Loading;
        public bool IsBusy => _request != null;
        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        private sealed class Request
        {
            public int Id;
            public readonly string BiId = Guid.NewGuid().ToString("N");
            public double Started;
            public bool Shown, Retried, Overlay = true;
            public IDisposable InputLock;
            public readonly UniTaskCompletionSource<bool> Result = new UniTaskCompletionSource<bool>();
        }

        private void Trace(string name, string status, string reason = "", Request request = null, double duration = 0)
        {
            Log.Info($"[Ad][{_adId}][instance={_instanceVersion}][request={request?.BiId}][load={_loadId}] {name} {status} {reason} duration={duration:F3}s");
        }

        // BI 独立于阶段日志：只记录实际展示和请求最终结果。
        private void ReportBi(string name, string status, Request request, string reason = "")
        {
            BiMgr.ReportEvent(name, new Dictionary<string, string>
            {
                { "ad_id", _adId }, { "status", status }, { "reason", reason ?? "" },
                { "ad_request_id", request.BiId },
                { "duration_sec", Math.Max(0, Now - request.Started).ToString("F3", CultureInfo.InvariantCulture) }
            });
        }
        private void SetState(RewardedAdState state)
        {
            Log.Info($"[Ad][{_adId}] state {State} -> {state}");
            State = state;
            AdSystem.NotifyStateChanged(_adId);
        }
        private bool IsCurrent(int version)
        {
            if (!_disposed && version == _instanceVersion) return true;
            Log.Info($"[Ad][{_adId}] ignored stale callback instance={version}, current={_instanceVersion}");
            return false;
        }
        public void EnterPage() { _pageActive = true; _retryCount = 0; Preload(); }
        public void ExitPage()
        {
            _pageActive = false;
            if (_request != null) Finish(_request, false, true, "page_exit");
        }
        public void Preload()
        {
            if (_disposed || IsBusy || IsLoading || IsAdAvailable) return;
            int load = ++_loadVersion;
            _loadId = Guid.NewGuid().ToString("N");
            _loadStarted = Now;
            _loadPending = true;
            SetState(RewardedAdState.Loading);
            Trace("ad_load", "start");
            try
            {
                if (_ad == null)
                {
                    int version = ++_instanceVersion;
                    _ad = SDK.CreateRewardedVideoAd(_adId,
                        () =>
                        {
                            if (!IsCurrent(version) || _request != null) return;
                            if (_loadPending) FinishLoad(true, "onLoad");
                            else SetState(RewardedAdState.Ready);
                        },
                        (code, message) =>
                        {
                            if (!IsCurrent(version)) return;
                            string error = code + ": " + message;
                            Trace("ad_error", "error", error, _request);
                            if (_request == null) FinishLoad(false, error);
                            else if (_request.Shown) Finish(_request, false, true, error);
                            // 展示尚未成功时由 Show 的失败回调重试；无回调则由15秒计时兜底。
                        },
                        rewarded =>
                        {
                            if (!IsCurrent(version) || _request == null) return;
                            var request = _request;
                            Trace("ad_close", rewarded ? "completed" : "interrupted", "onClose", request, Now - request.Started);
                            Finish(request, rewarded, false, rewarded ? "completed" : "interrupted");
                        });
                }
                int current = _instanceVersion;
                _ad.Load(() =>
                {
                    if (IsCurrent(current) && load == _loadVersion && _request == null) FinishLoad(true, "load_success");
                }, error =>
                {
                    if (IsCurrent(current) && load == _loadVersion && _request == null) FinishLoad(false, error);
                });
                WatchLoadAsync(load, current).Forget();
            }
            catch (Exception error) { FinishLoad(false, error.Message); }
        }
        private void FinishLoad(bool success, string reason)
        {
            if (!_loadPending) return;
            _loadPending = false;
            Trace("ad_load", success ? "success" : "failed", reason, null, Now - _loadStarted);
            SetState(success ? RewardedAdState.Ready : RewardedAdState.Failed);
            if (!success && _pageActive && _retryCount++ < 2) RetryLoadAsync(_instanceVersion).Forget();
        }
        private async UniTaskVoid WatchLoadAsync(int load, int version)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(AdSystem.LoadingTimeoutSeconds), DelayType.Realtime);
            if (!_disposed && version == _instanceVersion && load == _loadVersion && _loadPending)
                FinishLoad(false, "load_callback_timeout");
        }
        private async UniTaskVoid RetryLoadAsync(int version)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(10), DelayType.Realtime);
            if (!_disposed && _pageActive && version == _instanceVersion && State == RewardedAdState.Failed) Preload();
        }
        public async UniTask<bool> ShowRewardedAdAsync()
        {
            if (!IsAdAvailable) { Log.Info($"[Ad][{_adId}] show rejected state={State}"); return false; }
            var request = new Request { Id = AdSystem.NextRequestId(), Started = Now, InputLock = UIInteractionLock.Acquire() };
            _request = request;
            AdSystem.OpenOverlay(request.Id);
            SetState(RewardedAdState.Showing);
            Trace("ad_request", "start", "", request);
            WatchRequestAsync(request).Forget();
            OpenAndShowAsync(request).Forget();
            return await request.Result.Task;
        }
        private async UniTaskVoid OpenAndShowAsync(Request request)
        {
            try
            {
                await GameModule.UI.ShowUIAsyncAwait<UIAd>(request.Id);
                if (_request != request || !request.Overlay) return;
                ShowNative(request);
            }
            catch (Exception error) { Finish(request, false, true, "open_or_show_exception: " + error.Message); }
        }
        private void ShowNative(Request request)
        {
            Trace("ad_show", "start", request.Retried ? "retry" : "initial", request);
            _ad.Show(() =>
            {
                if (_request != request || request.Shown) return;
                request.Shown = true;
                Trace("ad_show", "success", "", request, Now - request.Started);
                ReportBi("ad_show", "shown", request);
            }, error =>
            {
                if (_request != request) return;
                Trace("ad_show", "failed", error, request, Now - request.Started);
                if (request.Retried) { Finish(request, false, true, error); return; }
                request.Retried = true;
                Trace("ad_reload", "start", "show_failed", request);
                try
                {
                    _ad.Load(() =>
                    {
                        if (_request != request) return;
                        Trace("ad_reload", "success", "", request);
                        try { ShowNative(request); }
                        catch (Exception ex) { Finish(request, false, true, ex.Message); }
                    }, failure =>
                    {
                        if (_request != request) return;
                        Trace("ad_reload", "failed", failure, request);
                        Finish(request, false, true, failure);
                    });
                }
                catch (Exception ex) { Finish(request, false, true, ex.Message); }
            });
        }
        private async UniTaskVoid WatchRequestAsync(Request request)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(AdSystem.LoadingTimeoutSeconds), DelayType.Realtime);
            if (_request != request) return;
            CloseOverlay(request);
            Trace("ad_overlay", "timeout", "15s_overlay_closed", request, Now - request.Started);
            if (!request.Shown) { Finish(request, false, true, "show_callback_timeout"); return; }
            // 正常视频可超过15秒；展示成功但永远没有close/error，也必须有限时结束请求。
            double remaining = AdSystem.CloseCallbackTimeoutSeconds - (Now - request.Started);
            if (remaining > 0) await UniTask.Delay(TimeSpan.FromSeconds(remaining), DelayType.Realtime);
            if (_request == request) Finish(request, false, true, "close_callback_timeout");
        }
        private void CloseOverlay(Request request)
        {
            if (!request.Overlay) return;
            request.Overlay = false;
            request.InputLock?.Dispose();
            request.InputLock = null;
            AdSystem.CloseOverlay(request.Id);
        }
        private void Finish(Request request, bool rewarded, bool resetNative, string reason)
        {
            if (_request != request) return;
            _request = null;
            Trace("ad_result", rewarded ? "rewarded" : "failed", reason, request, Now - request.Started);
            ReportBi("ad_result", rewarded ? "success" : "failed", request, reason);
            CloseOverlay(request);
            if (resetNative) ResetNative();
            SetState(RewardedAdState.Idle);
            request.Result.TrySetResult(rewarded);
            // 离开原生回调栈后再加载，避免在onClose里同步开始下一轮。
            if (_pageActive && !_disposed) PreloadNextAsync(_instanceVersion).Forget();
        }
        private async UniTaskVoid PreloadNextAsync(int version)
        {
            await UniTask.Delay(TimeSpan.FromMilliseconds(250), DelayType.Realtime);
            if (!_disposed && _pageActive && version == _instanceVersion) Preload();
        }
        public void Dispose()
        {
            _disposed = true;
            ExitPage();
            ResetNative();
            SetState(RewardedAdState.Idle);
        }
        private void ResetNative()
        {
            ++_instanceVersion;
            ++_loadVersion;
            _loadPending = false;
            var old = _ad;
            _ad = null;
            try { old?.Dispose(); }
            catch (Exception error) { Log.Warning($"[Ad][{_adId}] dispose failed: {error.Message}"); }
        }
    }
}
