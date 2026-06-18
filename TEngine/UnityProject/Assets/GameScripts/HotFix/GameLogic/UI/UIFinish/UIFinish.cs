using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.Top, location: "UIFinish")]
    public class UIFinish : UIWindow
    {
        private XYButton _btnNext;
        private XYButton _btnHome;
        private GameObject _btnNextGo;

        private int _completedLevelIndex;

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _btnNext = CreateWidget<XYButton>("BtnNext");
            _btnHome = CreateWidget<XYButton>("BtnHome");
            _btnNextGo = _btnNext?.gameObject;
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
