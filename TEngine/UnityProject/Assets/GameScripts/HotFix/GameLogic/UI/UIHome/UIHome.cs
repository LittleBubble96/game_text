
using System.Collections.Generic;
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
        private RectTransform _tabContentRect;

        private ETabType _curIndex = ETabType.None;
        
        private Dictionary<ETabType, UIHomeTabContentWidget> _tabContentWidgets = new Dictionary<ETabType, UIHomeTabContentWidget>();
        private Dictionary<ETabType ,HomeTabButtonWidget> _tabBtnWidgets = new Dictionary<ETabType, HomeTabButtonWidget>();
        protected override void ScriptGenerator()
        {
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
            OnSelectTab(index);
            _curIndex = index;
            if (_tabBtnWidgets.TryGetValue(index, out var unSelectBtnWidget))
            {
                unSelectBtnWidget.DoSelect();
            }
        }
        
        #endregion

        #region TabContent
        
        private void OnSelectTab(ETabType index)
        {
            bool isRight = index < _curIndex;
            if (_curIndex != ETabType.None)
            {
                _tabContentWidgets[_curIndex].OnExit(isRight);
            }
            if (!_tabContentWidgets.TryGetValue(index, out var widget))
            {
                widget = GenerateUiWidget(index);
                widget.OnInit(_tabContentRect);
                _tabContentWidgets.Add(index, widget);
            }
            widget.OnEnter(_curIndex == ETabType.None , isRight);
        }

        private UIHomeTabContentWidget GenerateUiWidget(ETabType index)
        {
            switch (index)
            {
                case ETabType.Level:
                    return CreateWidgetByPath<UIHomeLevelTabContentWidget>(_tabContentRect , UIHomeLevelTabContentWidget.LevelPrefabPath);
                case ETabType.Setting:
                    return CreateWidgetByPath<UIHomeSettingTabContentWidget>(_tabContentRect , UIHomeSettingTabContentWidget.SettingPrefabPath);
                default:
                    return null;
            }
        }

        #endregion
    }
}