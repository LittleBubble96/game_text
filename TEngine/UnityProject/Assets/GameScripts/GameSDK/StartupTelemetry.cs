using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace GameSDK
{
    /// <summary>启动层与热更层共用的启动漏斗；主线程调用，经统一 SDK 登录门禁上报。</summary>
    public static class StartupTelemetry
    {
        private static readonly string[] Steps = { "", "launch", "init_package", "request_version",
            "update_manifest", "create_downloader", "download_patch", "clear_cache", "preload",
            "load_assembly", "init_sdk", "init_config", "init_manager", "init_system", "load_top_ui",
            "load_home", "finalize_startup", "complete" };
        private sealed class StepState
        {
            public int Attempt;
            public long StartedAt;
            public bool Running;
        }
        private static readonly Dictionary<int, StepState> States = new Dictionary<int, StepState>();
        private static readonly Stopwatch Clock = new Stopwatch();
        private static string _startupId;
        private static string _appVersion;
        private static string _playMode;
        private static bool _completed;
        public static bool Enabled { get; set; } = true;
        public static string ResourceVersion { get; set; } = "";

        public static void Begin(string appVersion, string playMode)
        {
            States.Clear();
            _completed = false;
            ResourceVersion = "";
            _startupId = Guid.NewGuid().ToString("N");
            _appVersion = appVersion;
            _playMode = playMode;
            Clock.Restart();
            Start(1);
        }

        public static void Start(int index)
        {
            if (_startupId == null || _completed || index < 1 || index >= 17 || index == 10) return;
            if (!States.TryGetValue(index, out var state))
                States[index] = state = new StepState();
            if (state.Running) return;
            state.Attempt++;
            state.StartedAt = Clock.ElapsedMilliseconds;
            state.Running = true;
            Report(index, "start", state);
        }

        public static void Success(int index, Dictionary<string, string> extra = null)
        {
            Finish(index, "success", extra);
        }

        public static void Fail(int index, string code, string message)
        {
            Finish(index, "fail", new Dictionary<string, string> {
                { "error_code", code ?? "" },
                { "error_msg", string.IsNullOrEmpty(message) ? "" : message.Substring(0, Math.Min(256, message.Length)) }
            });
        }

        private static void Finish(int index, string status, Dictionary<string, string> extra)
        {
            if (_completed || !States.TryGetValue(index, out var state) || !state.Running) return;
            state.Running = false;
            Report(index, status, state, extra);
        }

        public static void Skip(int index, string reason)
        {
            if (_startupId == null || _completed || index < 1 || index >= 17 || index == 10 || States.ContainsKey(index)) return;
            var state = new StepState { StartedAt = Clock.ElapsedMilliseconds };
            States[index] = state;
            Report(index, "skip", state, new Dictionary<string, string> { { "reason", reason } });
        }

        public static void Complete()
        {
            if (_startupId == null || _completed) return;
            _completed = true;
            Report(17, "success", new StepState { Attempt = 1 }, null, "startup_complete");
        }

        private static void Report(int index, string status, StepState state,
            Dictionary<string, string> extra = null, string eventName = "startup_step")
        {
            if (!Enabled) return;
            long now = Clock.ElapsedMilliseconds;
            var data = new Dictionary<string, string> {
                { "startup_id", _startupId }, { "index", index.ToString(CultureInfo.InvariantCulture) },
                { "step", Steps[index] }, { "status", status },
                { "elapsed_ms", now.ToString(CultureInfo.InvariantCulture) },
                { "attempt", state.Attempt.ToString(CultureInfo.InvariantCulture) },
                { "app_version", _appVersion ?? "" }, { "resource_version", ResourceVersion ?? "" },
                { "play_mode", _playMode ?? "" }
            };
            if (status != "start") data["duration_ms"] = (now - state.StartedAt).ToString(CultureInfo.InvariantCulture);
            if (extra != null) foreach (var pair in extra) data[pair.Key] = pair.Value ?? "";
            SDK.ReportEvent(eventName, data);
        }
    }
}
