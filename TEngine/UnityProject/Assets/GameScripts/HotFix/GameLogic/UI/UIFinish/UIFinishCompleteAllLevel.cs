using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.Top, location: "UIFinishCompleteAllLevel")]
    public class UIFinishCompleteAllLevel : UIWindow
    {
        private RTLTextMeshPro _confirmBtnText;
        private RTLTextMeshPro _desText;
        private RTLTextMeshPro _title;
        private XYButton _btnConfirm;
        

        private Animation _animation;
        private string _animShowName = "UIFinishShowAnim";
        private string _animHideName = "UIFinishHideAnim";

        
        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _animation = transform.GetComponent<Animation>();
            _btnConfirm = CreateWidget<XYButton>("VictoryPanel/ButtonConfirm");
            _confirmBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonConfirm/Text");
            _title = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/Title");
            _desText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/m_des");
            _btnConfirm.OnAddListener(OnBtnConfirmClick);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            RefreshText();
        }

        protected override void OnInAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animShowName, CompleteInAnimation).Forget();
        }

        protected override void OnOutAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animHideName, CompleteOutAnimation).Forget();
        }
        
        #region 按钮交互
        

        private void OnBtnConfirmClick()
        {
            GameModule.UI.CloseUI<UIFinishCompleteAllLevel>();
        }

        #endregion

        private void RefreshText()
        {
            _title.text = LocalizationHelper.GetLocalText(LanguageKey.finish_completeAllTitle);
            _desText.text = LocalizationHelper.GetLocalText(LanguageKey.finish_completeDes);
            _confirmBtnText.text = LocalizationHelper.GetLocalText(LanguageKey.btn_confirm);
        }
    }
}
