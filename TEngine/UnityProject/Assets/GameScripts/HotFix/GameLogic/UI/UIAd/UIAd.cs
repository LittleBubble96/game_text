using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.System, location: "UIAd")]
    public class UIAd : UIWindow
    {
        private Transform _spinner;
        protected override void OnCreate()
        {
            base.OnCreate();
            _spinner = FindChildComponent<Transform>("Panel/root");
            // 使用不受 Time.timeScale 影响的旋转，避免原动画与代码叠加。
            var animation = _spinner != null ? _spinner.GetComponent<Animation>() : null;
            if (animation != null) animation.enabled = false;
        }
        protected override void OnRefresh()
        {
            base.OnRefresh();
            if (!AdSystem.HasOverlay) Close();
        }
        protected override void OnUpdate()
        {
            if (!AdSystem.HasOverlay) { Close(); return; }
            if (_spinner != null) _spinner.Rotate(0, 0, -240f * Time.unscaledDeltaTime);
        }
    }
}
