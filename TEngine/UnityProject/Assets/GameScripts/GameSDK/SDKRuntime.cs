using UnityEngine;

namespace GameSDK
{
    /// <summary>常驻主线程驱动登录超时、延迟重试和队列补发。</summary>
    public sealed class SDKRuntime : MonoBehaviour
    {
        private void Update() => SDK.Tick();
    }
}
