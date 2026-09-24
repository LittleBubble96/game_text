namespace GameLogic
{
    /// <summary>微信后台广告位 ID 集中配置；后续 Banner/插屏 ID 也放在这里。</summary>
    public static class AdIds
    {
        // 微信平台必须填真实 ID；编辑器由 EditorSDK 直接模拟成功。
        public static string RewardedVideo = "adunit-8b8635d5a8f2d6ae";
        public static string TipRewardedVideo = "adunit-8b8635d5a8f2d6ae";
        public static string NextRewardedVideo = "adunit-a800bc380ca2fe1c";
        // 可复用默认广告位，也可分别填不同 ID，管理器按最终 ID 隔离状态。
        public static string Tip => string.IsNullOrEmpty(TipRewardedVideo) ? RewardedVideo : TipRewardedVideo;
        public static string Next => string.IsNullOrEmpty(NextRewardedVideo) ? RewardedVideo : NextRewardedVideo;
    }
}
