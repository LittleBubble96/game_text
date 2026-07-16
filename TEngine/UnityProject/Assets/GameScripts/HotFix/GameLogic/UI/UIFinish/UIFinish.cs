using DG.Tweening;
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

        private Animation _animation;
        private string _animShowName = "UIFinishShowAnim";
        private string _animHideName = "UIFinishHideAnim";

        private int _completedLevelId;

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _animation = transform.GetComponent<Animation>();
            _btnNext = CreateWidget<XYButton>("VictoryPanel/ButtonNext");
            _btnHome = CreateWidget<XYButton>("VictoryPanel/ButtonBack");
            _nextBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonNext/Text");
            _homeBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonBack/Text");
            _btnNextGo = _btnNext.gameObject;
            _btnNext.OnAddListener(OnBtnNextClick);
            _btnHome.OnAddListener(OnBtnHomeClick);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            GameEvent.Send(EventDefine.Event_UITopUpdate, new UITopData(showCoin: true, showBack: false));

            if (_userDatas != null && _userDatas.Length > 0 && _userDatas[0] is int levelId)
            {
                _completedLevelId = levelId;
            }

            // 判断是否还有下一关（检查 levelId+1 是否存在于关卡表）
            bool hasNextLevel = GameManager.Instance.LevelConfig?.GetLevelNameByLevelId(_completedLevelId + 1) != null;
            if (_btnNextGo != null)
            {
                _btnNextGo.SetActive(hasNextLevel);
            }

            RefreshText();
        }

        protected override void OnInAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animShowName, base.OnInAnimation).Forget();
        }

        protected override void OnOutAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animHideName, base.OnOutAnimation).Forget();
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
