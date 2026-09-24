using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace GameLogic
{
    public static partial class BiMgr
    {
        // 仅用于计时，不上报 app_lifecycle；暂停期间不累计关卡、引导、结算时长。
        private static readonly Stopwatch ForegroundClock = Stopwatch.StartNew();
        private static Dictionary<string, string> _levelContext;
        private static bool _attemptActive;
        private static double _levelStartedAt;
        private static double _levelDuration;
        private static int _foundCount;
        private static int _submitCount;
        private static int _wrongCount;
        private static int _tipCount;
        private static bool _finishShown;
        private static bool _finishActionSent;
        private static double _finishShownAt;
        private static bool _guideActive;
        private static double _guideStartedAt;

        private static double Now => ForegroundClock.Elapsed.TotalSeconds;
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Seconds(double value) => Math.Max(0, value).ToString("F3", CultureInfo.InvariantCulture);

        public static void SetPaused(bool paused)
        {
            if (paused) ForegroundClock.Stop();
            else ForegroundClock.Start();
        }

        private static Dictionary<string, string> Context()
        {
            return _levelContext == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(_levelContext);
        }

        private static Dictionary<string, string> Progress()
        {
            var data = Context();
            data["duration_sec"] = Seconds(_attemptActive ? Now - _levelStartedAt : _levelDuration);
            data["found_count"] = Number(_foundCount);
            data["submit_count"] = Number(_submitCount);
            data["wrong_count"] = Number(_wrongCount);
            data["tip_count"] = Number(_tipCount);
            return data;
        }

        public static void LevelStarted(int levelId, string levelName, string baseCharacter,
            string entry, bool restored, int foundCount, int requiredCount)
        {
            _levelContext = new Dictionary<string, string>
            {
                { "level_id", Number(levelId) }, { "level_name", levelName ?? "" },
                { "base_character", baseCharacter ?? "" }, { "attempt_id", Guid.NewGuid().ToString("N") },
                { "required_count", Number(requiredCount) }
            };
            _attemptActive = true;
            _levelStartedAt = Now;
            _levelDuration = 0;
            _foundCount = foundCount;
            _submitCount = _wrongCount = _tipCount = 0;
            _finishShown = _finishActionSent = _guideActive = false;
            var data = Progress();
            data["entry"] = entry;
            data["restored"] = restored ? "1" : "0";
            ReportEvent("level_start", data);
        }

        public static void AnswerSubmitted(string result, string answer, int foundCount, int strokeCount)
        {
            if (!_attemptActive) return;
            _foundCount = foundCount;
            _submitCount++;
            if (result == "wrong") _wrongCount++;
            var data = Progress();
            data["result"] = result;
            data["answer"] = answer ?? "";
            data["stroke_count"] = Number(strokeCount);
            ReportEvent("answer_submit", data);
        }

        public static void LevelCompleted(int foundCount, string completionMethod)
        {
            if (!_attemptActive) return;
            _foundCount = foundCount;
            _levelDuration = Now - _levelStartedAt;
            _attemptActive = false;
            var data = Progress();
            data["completion_method"] = completionMethod;
            ReportEvent("level_complete", data);
        }

        public static void LeaveLevelForHome(int foundCount)
        {
            if (_attemptActive)
            {
                _foundCount = foundCount;
                _levelDuration = Now - _levelStartedAt;
                _attemptActive = false;
                var data = Progress();
                data["reason"] = "home";
                ReportEvent("level_leave", data);
            }
            // 主页领取资源不应携带上一关的 attempt_id。
            _levelContext = null;
            _guideActive = _finishShown = false;
        }

        public static void PropUsed(string prop, string payment, int cost, bool success, string reason)
        {
            if (success && prop == "tip" && _attemptActive) _tipCount++;
            var data = Progress();
            data["prop"] = prop;
            data["payment"] = payment;
            data["cost"] = Number(cost);
            data["result"] = success ? "success" : "failed";
            data["reason"] = reason;
            ReportEvent("prop_use", data);
        }

        public static void ResourceChanged(string resource, int before, int after, string source)
        {
            if (before == after) return;
            var data = Context();
            data["resource"] = resource;
            data["delta"] = ((long)after - before).ToString(CultureInfo.InvariantCulture);
            data["balance"] = Number(after);
            data["source"] = source;
            ReportEvent("resource_change", data);
        }

        public static void GuideStarted()
        {
            if (_guideActive) return;
            _guideActive = true;
            _guideStartedAt = Now;
            GuideStep("start");
        }

        public static void GuideStep(string step)
        {
            if (!_guideActive) return;
            var data = Context();
            data["step"] = step;
            data["duration_sec"] = Seconds(Now - _guideStartedAt);
            ReportEvent("guide_step", data);
            if (step == "complete") _guideActive = false;
        }

        public static void ShareClicked(string entry)
        {
            var data = Context();
            data["entry"] = entry;
            ReportEvent("share_click", data);
        }

        public static void FinishShown()
        {
            if (_finishShown) return;
            _finishShown = true;
            _finishShownAt = Now;
            _finishActionSent = false;
        }

        public static void FinishAction(string action)
        {
            if (!_finishShown || _finishActionSent) return;
            _finishActionSent = true;
            var data = Context();
            data["action"] = action;
            data["duration_sec"] = Seconds(Now - _finishShownAt);
            ReportEvent("finish_action", data);
        }

        public static void HomeAction(string action, int levelId)
        {
            ReportEvent("home_action", new Dictionary<string, string>
            {
                { "action", action }, { "level_id", Number(levelId) }
            });
        }

        public static void AllLevelsCompleted(int lastLevelId, string contentVersion)
        {
            var data = Context();
            data["last_level_id"] = Number(lastLevelId);
            data["content_version"] = contentVersion;
            ReportEvent("all_levels_complete", data);
        }
    }
}
