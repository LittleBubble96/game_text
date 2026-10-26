using System;
using System.Collections.Generic;
using GameSDK;

namespace GameLogic
{
    /// <summary>关卡事件去重与有效前台计时；后台和广告暂停独立叠加。</summary>
    internal sealed class LevelReportTracker
    {
        private readonly Func<double> _now;
        private readonly Action<LevelReport> _report;
        private readonly HashSet<int> _ads = new HashSet<int>();
        private bool _paused, _active;
        private double _updatedAt, _elapsed;
        private int _levelId, _requiredCount, _foundCount, _gameVersion;
        private string _levelName;

        public LevelReportTracker(Func<double> now, Action<LevelReport> report)
        { _now = now; _report = report; }

        private void UpdateTime()
        {
            double now = _now();
            if (_active && !_paused && _ads.Count == 0) _elapsed += Math.Max(0, now - _updatedAt);
            _updatedAt = now;
        }

        public void SetPaused(bool paused) { UpdateTime(); _paused = paused; }
        public void SetAdShowing(int requestId, bool showing)
        {
            UpdateTime();
            if (showing) _ads.Add(requestId);
            else _ads.Remove(requestId);
        }

        public void Start(int levelId, string levelName, int foundCount, int requiredCount, int loginCount, int gameVersion)
        {
            _levelId = levelId;
            _levelName = levelName;
            _foundCount = foundCount;
            _requiredCount = requiredCount;
            _gameVersion = gameVersion;
            _elapsed = 0;
            _updatedAt = _now();
            _active = true;
            Send(LevelReportResult.Enter, loginCount);
        }

        public void UpdateProgress(int foundCount) { _foundCount = Math.Max(0, foundCount); }
        public void PropUsed() { if (_active) Send(LevelReportResult.UseProp); }
        public void Complete(int foundCount) => End(LevelReportResult.Success, foundCount);
        public void Leave(int foundCount) => End(LevelReportResult.Exit, foundCount);

        private void End(LevelReportResult result, int foundCount)
        {
            if (!_active) return;
            UpdateTime();
            UpdateProgress(foundCount);
            _active = false;
            Send(result);
        }

        private void Send(LevelReportResult result, int loginCount = 1)
        {
            int progress = _requiredCount > 0 ? (int)Math.Min(100L, (long)_foundCount * 100 / _requiredCount) : 0;
            if (result == LevelReportResult.Success) progress = 100;
            _report(new LevelReport(_levelId, _levelName, result, progress, _gameVersion,
                (int)Math.Min(int.MaxValue, Math.Floor(_elapsed)), loginCount));
        }
    }
}
