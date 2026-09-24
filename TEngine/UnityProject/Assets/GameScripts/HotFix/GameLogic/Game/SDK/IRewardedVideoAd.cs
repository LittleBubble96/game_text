using System;

namespace GameLogic
{
    /// <summary>平台无关的单广告位实例；原生类型不流出 SDK 层。</summary>
    public interface IRewardedVideoAd : IDisposable
    {
        void Load(Action onLoaded, Action<string> onFailed);
        void Show(Action onShown, Action<string> onFailed);
    }
}
