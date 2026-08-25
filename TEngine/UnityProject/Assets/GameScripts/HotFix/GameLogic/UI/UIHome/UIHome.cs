
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.Localization;
using GameLogic.UI;
using RTLTMPro;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    public enum ETabType
    {
        None = 0,
        Level = 1,
        Setting = 2,
        Store = 3,
    }

    [Window(UILayer.UI,location:"UIHome")]
    class UIHome : UIWindow
    {
        #region 脚本工具生成的代码

        private Animation _animation;
        private RectTransform _tabContentRect;

        private ETabType _curIndex = ETabType.None;
        
        private Dictionary<ETabType, UIHomeTabContentWidget> _tabContentWidgets = new Dictionary<ETabType, UIHomeTabContentWidget>();
        private Dictionary<ETabType ,HomeTabButtonWidget> _tabBtnWidgets = new Dictionary<ETabType, HomeTabButtonWidget>();

        private string _showAnim = "Ui_HomeShow";
        private string _hideAnim = "Ui_HomeHide";
        
        protected override void ScriptGenerator()
        {
            _animation = transform.GetComponent<Animation>();
            RectTransform btnLayout = FindChildComponent<RectTransform>("Panel/BottomLayout");
            _tabContentRect = FindChildComponent<RectTransform>("Panel/Content");
            for (int i = 0; i < btnLayout.childCount; i++)
            {
                ETabType tabType = (ETabType)(i + 1);
                GameObject childObj = btnLayout.GetChild(i).gameObject;
                HomeTabButtonWidget widget = CreateWidget<HomeTabButtonWidget>(childObj , childObj.activeInHierarchy);
                widget.Init(tabType , OnTabBtnClick);
                _tabBtnWidgets.Add(tabType , widget);
            }
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
            OnTabBtnClick(ETabType.Level);
        }

        #endregion

        #region 事件

        private void OnTabBtnClick(ETabType index)
        {
            if (_curIndex == index)
            {
                return;
            }
            if (_tabBtnWidgets.TryGetValue(_curIndex, out var btnWidget))
            {
                btnWidget.DoUnSelect();
            }
            OnSelectTab(index).Forget();
            _curIndex = index;
            if (_tabBtnWidgets.TryGetValue(index, out var unSelectBtnWidget))
            {
                unSelectBtnWidget.DoSelect();
            }
        }
        
        #endregion

        #region TabContent

        private void EnableTabBtn(bool enable)
        {
            foreach (var tabBtnWidget in _tabBtnWidgets)
            {
                tabBtnWidget.Value.EnableBtn(enable);
            }
        }

        private async UniTaskVoid OnSelectTab(ETabType index)
        {
            EnableTabBtn(false);
            bool isRight = index < _curIndex;
            if (_curIndex != ETabType.None)
            {
                _tabContentWidgets[_curIndex].OnExit(isRight);
            }
            if (!_tabContentWidgets.TryGetValue(index, out var widget))
            {
                widget = await GenerateUiWidget(index);
                widget.OnInit(_tabContentRect);
                _tabContentWidgets.Add(index, widget);
            }
            widget.OnEnter(_curIndex == ETabType.None , isRight);
            EnableTabBtn(true);
        }

        private async UniTask<UIHomeTabContentWidget> GenerateUiWidget(ETabType index)
        {
            switch (index)
            {
                case ETabType.Level:
                    return await CreateWidgetByPathAsync<UIHomeLevelTabContentWidget>(_tabContentRect , UIHomeLevelTabContentWidget.LevelPrefabPath);
                case ETabType.Setting:
                    return await CreateWidgetByPathAsync<UIHomeSettingTabContentWidget>(_tabContentRect , UIHomeSettingTabContentWidget.SettingPrefabPath);
                default:
                    return null;
            }
        }

        #endregion
    }
}