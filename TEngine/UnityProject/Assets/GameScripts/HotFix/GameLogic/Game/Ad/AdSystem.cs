using System;

namespace GameLogic
{
    /// <summary>
    /// 广告系统占位 —— 预留激励广告扩展点。
    /// 当前未接入真实 SDK，IsAdAvailable 固定返回 false、ShowRewardedAd 直接回调失败。
    /// 未来接入微信/抖音等激励广告后，在此实现真实逻辑即可，调用方无需改动。
    /// </summary>
    public static class AdSystem
    {
        /// <summary>激励广告是否可用（未接入时固定 false）</summary>
        public static bool IsAdAvailable => false;

        /// <summary>
        /// 播放激励广告，完成后回调是否成功（未接入时直接回调 false）。
        /// </summary>
        /// <param name="onCompleted">true=观看完成可发奖；false=未完成/无广告/取消</param>
        public static void ShowRewardedAd(Action<bool> onCompleted)
        {
            // TODO: 接入激励广告 SDK（微信/抖音等），按播放结果回调 onCompleted
            onCompleted?.Invoke(false);
        }
    }
}
