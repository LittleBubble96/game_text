using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.Top, location: "UIFinish")]
    public class UIFinish : UIWindow
    {
        private RTLTextMeshPro _nextBtnText;
        private RTLTextMeshPro _homeBtnText;
        private XYButton _btnNext;
        private XYButton _btnHome;
        private GameObject _btnNextGo;

        private int _completedLevelIndex;

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _btnNext = CreateWidget<XYButton>("BtnNext");
            _btnHome = CreateWidget<XYButton>("BtnHome");
            _nextBtnText = this.FindChildComponent<RTLTextMeshPro>("BtnNext/m_text");
            _homeBtnText = this.FindChildComponent<RTLTextMeshPro>("BtnHome/m_text");
            _btnNextGo = _btnNext.gameObject;
            _btnNext.OnAddListener(OnBtnNextClick);
            _btnHome.OnAddListener(OnBtnHomeClick);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();

            if (_userDatas != null && _userDatas.Length > 0 && _userDatas[0] is int levelIndex)
            {
                _completedLevelIndex = levelIndex;
            }

            // 判断是否还有下一关
            bool hasNextLevel = _completedLevelIndex + 1 < (GameManager.Instance.LevelConfig?.LevelCount ?? 0);
            if (_btnNextGo != null)
            {
                _btnNextGo.SetActive(hasNextLevel);
            }

            RefreshText();
        }

        private void RefreshText()
        {
            _nextBtnText.text = LocalizationHelper.GetLocalText(LanguageKey.next_level_btn);
            _homeBtnText.text = LocalizationHelper.GetLocalText(LanguageKey.back_btn);
        }

        private void OnBtnNextClick()
        {
            GameModule.UI.CloseUI<UIFinish>();
            GameManager.Instance.LoadNextCorePlayLevel();
        }

        private void OnBtnHomeClick()
        {
            GameManager.Instance.ReturnToHome();
        }
    }
}
