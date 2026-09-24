using GameLogic.UI;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.UI,location:"UIHome")]
    class UIHome : UIWindow
    {
        #region 脚本工具生成的代码

        private Animation _animation;
        private RectTransform _tabContentRect;
        private UIHomeLevelTabContentWidget _levelTabContentWidget; 
        

        private string _showAnim = "Ui_HomeShow";
        private string _hideAnim = "Ui_HomeHide";
        
        protected override void ScriptGenerator()
        {
            _animation = transform.GetComponent<Animation>();
            _levelTabContentWidget = CreateWidget<UIHomeLevelTabContentWidget>("Panel/Content/UIHome_LevelTabContent");
        }

        protected override void OnInAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_showAnim, OnInAnimationComplete).Forget();
        }

        protected override void OnOutAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_hideAnim, OnOutAnimationComplete).Forget();
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            GameEvent.Send(EventDefine.Event_UITopUpdate, new UITopData(showCoin: true, showBack: false));
            GameEvent.Send(EventDefine.Event_UITopCoinUpdate, PropDefine.CoinCount);
            _levelTabContentWidget.RefreshLevelInfo();
        }

        #endregion
    }
}
