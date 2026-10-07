using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameSDK
{
    public enum SdkLoginState { NotStarted, LoggingIn, LoggedIn, Failed }

    /// <summary>主线程使用。登录门禁、FIFO 缓存及有限重试的唯一所有者。</summary>
    internal sealed class SdkSession
    {
        private sealed class Event
        {
            public string Name;
            public Dictionary<string, string> Data;
            public int Failures;
            public bool NativeGameStart;
        }
        private readonly Queue<Event> _pending = new Queue<Event>();
        private readonly Func<double> _now;
        private readonly Action<string> _warn;
        private readonly string _sessionId = Guid.NewGuid().ToString("N");
        private ISdk _platform;
        private int _generation;
        private int _attempt;
        private int _cycleAttempts;
        private long _sequence;
        private double _loginDeadline;
        private double _retryLoginAt = double.PositiveInfinity;
        private double _retryReportAt;
        private bool _flushing;
        public SdkLoginState LoginState { get; private set; }
        public bool IsLoggedIn => LoginState == SdkLoginState.LoggedIn;
        public string OpenId { get; private set; } = "";
        public int PendingCount => _pending.Count;

        public SdkSession(Func<double> now, Action<string> warn) { _now = now; _warn = warn; }
        public void Initialize(ISdk platform)
        {
            if (_platform != null) return;
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            StartLogin();
        }
        public void RetryLogin()
        {
            if (_platform == null || IsLoggedIn || LoginState == SdkLoginState.LoggingIn) return;
            _cycleAttempts = 0;
            StartLogin();
        }
        private void StartLogin()
        {
            LoginState = SdkLoginState.LoggingIn;
            OpenId = "";
            int token = ++_generation;
            _attempt++;
            _cycleAttempts++;
            _retryLoginAt = double.PositiveInfinity;
            _loginDeadline = _now() + 60;
            LoginEvent("start", "");
            try
            {
                _platform.Login(id =>
                {
                    if (token != _generation || LoginState != SdkLoginState.LoggingIn) return;
                    if (string.IsNullOrWhiteSpace(id)) { LoginFailed(token, "empty_openid"); return; }
                    OpenId = id;
                    LoginState = SdkLoginState.LoggedIn;
                    _retryReportAt = 0;
                    LoginEvent("success", "");
                    Flush();
                }, error => LoginFailed(token, error));
            }
            catch (Exception e) { LoginFailed(token, e.Message); }
        }
        private void LoginFailed(int token, string error)
        {
            if (token != _generation || LoginState != SdkLoginState.LoggingIn) return;
            LoginState = SdkLoginState.Failed;
            LoginEvent("fail", error);
            _warn("[SDK] Login failed: " + error);
            if (_cycleAttempts < 3) _retryLoginAt = _now() + Math.Pow(2, _cycleAttempts);
        }
        private void LoginEvent(string status, string error)
        {
            ReportEvent("sdk_login", new Dictionary<string, string> {
                { "status", status }, { "attempt", _attempt.ToString(CultureInfo.InvariantCulture) },
                { "error_msg", string.IsNullOrEmpty(error) ? "" : error.Substring(0, Math.Min(256, error.Length)) }
            });
        }
        /// <returns>已接收至本地发送队列，不代表服务端确认。</returns>
        public bool ReportEvent(string name, Dictionary<string, string> data)
            => Enqueue(name, data, false);

        public bool ReportGameStart() => Enqueue("native_game_start", null, true);

        private bool Enqueue(string name, Dictionary<string, string> data, bool nativeGameStart)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (_pending.Count >= 512) { _warn("[BI] Queue full; dropped new event: " + name); return false; }
            var copy = new Dictionary<string, string>();
            if (data != null) foreach (var pair in data)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) return false;
                copy.Add(pair.Key, pair.Value ?? "");
            }
            copy["sdk_session_id"] = _sessionId;
            copy["event_seq"] = (++_sequence).ToString(CultureInfo.InvariantCulture);
            copy["event_time_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            _pending.Enqueue(new Event { Name = name, Data = copy, NativeGameStart = nativeGameStart });
            Flush();
            return true;
        }
        public void Tick()
        {
            if (LoginState == SdkLoginState.LoggingIn && _now() >= _loginDeadline)
                LoginFailed(_generation, "login_timeout");
            if (LoginState == SdkLoginState.Failed && _now() >= _retryLoginAt) StartLogin();
            Flush();
        }
        public void RetryPendingEvents()
        {
            if (_pending.Count > 0) _pending.Peek().Failures = 0;
            _retryReportAt = 0;
            Flush();
        }
        private void Flush()
        {
            if (!IsLoggedIn || _flushing || _now() < _retryReportAt) return;
            _flushing = true;
            try
            {
                int budget = 512;
                while (_pending.Count > 0 && budget-- > 0)
                {
                    var item = _pending.Peek();
                    try
                    {
                        if (item.NativeGameStart) _platform.ReportGameStart();
                        else _platform.ReportEvent(item.Name, new Dictionary<string, string>(item.Data));
                    }
                    catch (Exception e)
                    {
                        item.Failures++;
                        _retryReportAt = item.Failures < 3 ? _now() + Math.Pow(2, item.Failures) : double.PositiveInfinity;
                        _warn("[BI] Report failed; event retained: " + e.Message);
                        break;
                    }
                    _pending.Dequeue();
                    _retryReportAt = 0;
                }
            }
            finally { _flushing = false; }
        }
    }
}
