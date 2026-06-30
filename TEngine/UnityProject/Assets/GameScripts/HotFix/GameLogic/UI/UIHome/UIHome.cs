
using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.UI,location:"UIHome")]
    class UIHome : UIWindow
    {
        #region 脚本工具生成的代码
        private XYButton  _playBtn;
        private RTLTextMeshPro _playBtnName;
       
        protected override void ScriptGenerator()
        {
            _playBtn = CreateWidget<XYButton>("m_btnStartLevel");
            _playBtnName = this.FindChildComponent<RTLTextMeshPro>("m_btnStartLevel/m_btnName");
            _playBtn.OnAddListener(OnStartLevel);
        }

        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            RefreshText();
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            RefreshText();
        }

        #endregion

        #region 事件

        private void RefreshText()
        {
            _playBtnName.text = LocalizationHelper.GetLocalText(LanguageKey.start_game_btn);
        }

        private void OnStartLevel()
        {
            GameManager.Instance.StartGame();
        }
        
        #endregion

    }
}