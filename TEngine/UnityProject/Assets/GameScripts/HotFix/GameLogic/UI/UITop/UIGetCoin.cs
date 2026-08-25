using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.UI, location: "UIGetCoin")]
    public class UIGetCoin : UIWindow
    {
        private RTLTextMeshPro _title;
        private RTLTextMeshPro _newDayCoinDes;
        private RTLTextMeshPro _newDayCoinBtnTmp;
        private RTLTextMeshPro _shareDayCoinDes;
        private RTLTextMeshPro _shareDayCoinBtnTmp;

        private XYButton _newDayBtn;
        private XYButton _shareDayBtn;
        private XYButton _closeBtn;
        private XYButton _fullCloseBtn;

        private Image _newDayBtnImg;
        private Image _shareDayBtnImg;

        private Animation _animation;
        private string _animShowName = "UIGetCoinShowAnim";
        private string _animHideName = "UIGetCoinHideAnim";
        
        private CommonGameSystem CommonGameSystem => GameSystem.GetSystem<CommonGameSystem>();

        protected override void OnCreate()
        {
            base.OnCreate();
            _animation = rectTransform.GetComponent<Animation>();
            _title = FindChildComponent<RTLTextMeshPro>("Panel/Title");
            _newDayCoinDes = FindChildComponent<RTLTextMeshPro>("Panel/DayRoot/m_des");
            _newDayCoinBtnTmp = FindChildComponent<RTLTextMeshPro>("Panel/DayRoot/GetBtn/m_text");
            _shareDayCoinDes = FindChildComponent<RTLTextMeshPro>("Panel/ShareRoot/m_des");
            _shareDayCoinBtnTmp = FindChildComponent<RTLTextMeshPro>("Panel/ShareRoot/GetBtn/m_text");
            _newDayBtn = CreateWidget<XYButton>("Panel/DayRoot/GetBtn");
            _shareDayBtn = CreateWidget<XYButton>("Panel/ShareRoot/GetBtn");
            _closeBtn = CreateWidget<XYButton>("Panel/CloseBtn");
            _fullCloseBtn = CreateWidget<XYButton>("fullBtn");
            _newDayBtnImg = FindChildComponent<Image>("Panel/DayRoot/GetBtn/bg");
            _shareDayBtnImg = FindChildComponent<Image>("Panel/ShareRoot/GetBtn/bg");

            _newDayBtn.OnAddListener(OnAddNewDay);
            _shareDayBtn.OnAddListener(OnAddShare);
            _closeBtn.OnAddListener(OnCloseUI);
            _fullCloseBtn.OnAddListener(OnCloseUI);
        }

        private void OnCloseUI()
        {
            GameModule.UI.CloseUI<UIGetCoin>();
        }

        private void OnAddNewDay()
        {
            CommonGameSystem.ClaimNewDayCoin();
            RefreshNewDay();
            int coinCount = GameDefine.NewDayCoinByDay;
            GameEvent.Send(EventDefine.Event_UITopCoinAddAnim , coinCount);
            PropDefine.AddCoin(coinCount);
        }

        private void OnAddShare()
        {
            CommonGameSystem.ShareNewDay();
            SDK.ShareAppMessage("一起来玩");
            RefreshShare();
            int coinCount = GameDefine.ShareCoinByDay;
            GameEvent.Send(EventDefine.Event_UITopCoinAddAnim , coinCount);
            PropDefine.AddCoin(coinCount);
        }
        
        protected override void OnInAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animShowName, CompleteInAnimation).Forget();
        }

        protected override void OnOutAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animHideName, CompleteOutAnimation).Forget();
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            _title.text = LocalizationHelper.GetLocalText(LanguageKey.getCoin_Title);
            RefreshNewDay();
            RefreshShare();
        }

        private void RefreshNewDay()
        {
            bool canClaim = CommonGameSystem.CanClaimNewDayCoin();
            EnableBtn(_newDayBtn, canClaim);
            _newDayCoinDes.text = LocalizationHelper.GetLocalText(canClaim ? LanguageKey.getCoin_Body_unClaim : LanguageKey.getCoin_Body_claim);
            _newDayCoinBtnTmp.text = LocalizationHelper.GetLocalText(canClaim ? LanguageKey.getCoin_Btn_unClaim : LanguageKey.getCoin_Btn_claim);
        }

        private void RefreshShare()
        {
            int shareRemindCount = CommonGameSystem.GetShareNewDayCount();
            bool canShare = shareRemindCount > 0;
            EnableBtn(_shareDayBtn, canShare);
            _shareDayCoinDes.text = canShare ? 
                string.Format(LocalizationHelper.GetLocalText(LanguageKey.getCoin_Share_Body_unClaim) , GameDefine.ShareCoinByDay , shareRemindCount) : 
                LocalizationHelper.GetLocalText(LanguageKey.getCoin_Share_Body_claim);
            _shareDayCoinBtnTmp.text = canShare ? 
                LocalizationHelper.GetLocalText(LanguageKey.getCoin_Share_Btn_unClaim) : 
                LocalizationHelper.GetLocalText(LanguageKey.getCoin_Share_Btn_claim);
        }

        private void EnableBtn(XYButton btn ,  bool enable)
        {
            btn.Interactable = enable;
            Image buttonBg = btn == _newDayBtn ? _newDayBtnImg : _shareDayBtnImg;
            buttonBg.color = enable ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.5f);
        }
    }
}