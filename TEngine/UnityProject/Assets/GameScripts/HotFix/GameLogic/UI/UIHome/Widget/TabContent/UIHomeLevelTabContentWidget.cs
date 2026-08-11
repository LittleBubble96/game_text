using Cysharp.Threading.Tasks;
using GameLogic.Localization;
using RTLTMPro;
using TMPro;
using UnityEngine;

namespace GameLogic.UI
{
    public class UIHomeLevelTabContentWidget : UIHomeTabContentWidget
    {
        public const string LevelPrefabPath = "UIHome_LevelTabContent";
        
        private XYButton  _playBtn;
        private RTLTextMeshPro _playBtnName;

        protected override void OnCreate()
        {
            base.OnCreate();
            _playBtn = CreateWidget<XYButton>("m_btnStartLevel");
            _playBtnName = this.FindChildComponent<RTLTextMeshPro>("m_btnStartLevel/m_btnName");
            _playBtn.OnAddListener(OnStartLevel);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            RefreshText();
            EnableBtn(true);
        }

        internal override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            RefreshText();
        }

        private void RefreshText()
        {
            _playBtnName.text = LocalizationHelper.GetLocalText(LanguageKey.start_game_btn);
        }
        

        private void EnableBtn(bool enable)
        {
            _playBtn.Enable = enable;
        }

        private void OnStartLevel()
        {
            EnableBtn(false);
            GameManager.Instance.StartGame().Forget();
        }
    }
}