using System.Collections.Generic;

namespace GameLogic
{
    /// <summary>一份通关奖励快照；每次结算只能领取一次广告补发，旧回调不能领取新关奖励。</summary>
    public sealed class LevelCompletionReward
    {
        private int _levelId;
        private bool _claimed;
        private Dictionary<int, int> _rewards;
        public int Version { get; private set; }

        public void Begin(int levelId, Dictionary<int, int> rewards)
        {
            Version++;
            _levelId = levelId;
            _claimed = false;
            _rewards = new Dictionary<int, int>();
            if (rewards != null)
                foreach (var reward in rewards)
                    if (reward.Value > 0) _rewards[reward.Key] = reward.Value;
        }

        public bool CanClaim(int levelId, int version) => !_claimed && version == Version &&
            levelId == _levelId && _rewards != null && _rewards.Count > 0;

        public bool HasClaimed(int levelId, int version) => _claimed && version == Version && levelId == _levelId;

        public bool TryClaim(int levelId, int version, bool adCompleted, out Dictionary<int, int> rewards)
        {
            rewards = null;
            if (!adCompleted || !CanClaim(levelId, version)) return false;
            _claimed = true;
            rewards = new Dictionary<int, int>(_rewards);
            return true;
        }
    }
}
