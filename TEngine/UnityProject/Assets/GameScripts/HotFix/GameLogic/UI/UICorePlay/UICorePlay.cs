using System.Collections.Generic;
using GameLogic.GamePlay.CorePlay;
using GameLogic.Localization;
using TEngine;
using TMPro;
using UnityEngine;
using RTLTMPro;

namespace GameLogic
{
    /// <summary>
    /// CorePlay 的 Canvas UI 管理 —— 通过 IGamePlay 接口获取数据，不依赖具体实现
    /// </summary>
    [Window(UILayer.UI, location: "UICorePlay")]
    public class UICorePlay : UIWindow
    {
        private RTLTextMeshPro _levelNameText;

        // private RTLTextMeshPro _answerDisplayText;
        private RTLTextMeshPro _answerProgressText;
        private RTLTextMeshPro _submitBtnText;

        private XYButton _submitButton;
        
        private TMP_Text _resultTipText;

        private float _resultTipDuration = 2f;

        private float _resultTipTimer;
        private bool _isShowingTip;
        
        private CorePlayLayoutWidget _layoutWidget;
        
        private CorePlayPropWidget _propWidget;
        

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _levelNameText = FindChildComponent<RTLTextMeshPro>("Panel/Titile");
            // _answerDisplayText = FindChildComponent<RTLTextMeshPro>("Answer");
            _answerProgressText = FindChildComponent<RTLTextMeshPro>("Panel/AnswerProgress");
            _submitBtnText = FindChildComponent<RTLTextMeshPro>("Panel/Buttom/SubmitBtn/bg/m_text");
            _submitButton = CreateWidget<XYButton>("Panel/Buttom/SubmitBtn");
            _resultTipText = FindChildComponent<TMP_Text>("Panel/ResultTip");
            _layoutWidget = CreateWidget<CorePlayLayoutWidget>("Panel/Layout");
            _propWidget = CreateWidget<CorePlayPropWidget>("Panel/Buttom/Props/TipProp");
            _submitButton.OnAddListener(OnSubmit);
        }
        
        // ================ 初始化 ================

        private void OnSubmit()
        {
            GameManager.Instance.CurrentView.OnSubmitClicked();
        }

        protected override void RegisterEvent()
        {
            base.RegisterEvent();
            AddUIEvent<bool, string, string>(EventDefine.Event_AnswerSubmitted, OnAnswerSubmitted);
        }
        
        protected override void OnRefresh()
        {
            base.OnRefresh();
            GameEvent.Send(EventDefine.Event_UITopUpdate, new UITopData(showCoin: true, showBack: true));
            GameEvent.Send(EventDefine.Event_UITopCoinUpdate, PropDefine.CoinCount);
            if (_resultTipText != null)
                _resultTipText.gameObject.SetActive(false);
            // 更新 UI
            if (GameManager.Instance.CurrentGamePlay is CorePlayGamePlay corePlay)
            {
                RefreshAnswerDisplay(corePlay.GetFoundAnswerCharacters(), corePlay.GetRequiredAnswerCount());
            }
            RefreshText();
            _layoutWidget.Activate();
        }

        private void RefreshText()
        {
            _submitBtnText.text = LocalizationHelper.GetLocalText(LanguageKey.submit_btn);
            SetLevelName();
        }

        protected override void OnUpdate()
        {
            if (_isShowingTip)
            {
                _resultTipTimer -= Time.deltaTime;
                if (_resultTipTimer <= 0) HideResultTip();
            }
        }
        
        // ================ 关卡信息 ================

        /// <summary>获取当前关卡名称</summary>
        private string GetLevelName()
        {
            if (GameManager.Instance.CurrentGamePlay == null) return "";
            int level = GameManager.Instance.CurrentGamePlay.CurrentLevelId;
            return string.Format(LocalizationHelper.GetLocalText(LanguageKey.level_title), level, GameManager.Instance.CurrentGamePlay.CurrentLevelData.baseCharacter);
        }
        
        private void OnAnswerSubmitted(bool success, string answerCharacter, string message)
        {
            ShowSubmitResult(success, answerCharacter, message);
        }

        private void SetLevelName()
        {
            if (_levelNameText != null)
                _levelNameText.text = GetLevelName();
        }

        // ================ 答案显示 ================

        public void RefreshAnswerDisplay(List<string> foundAnswers, int requiredCount)
        {
            // if (_answerDisplayText != null)
            // {
            //     _answerDisplayText.text = string.Join("  ", foundAnswers);
            // }

            if (_answerProgressText != null)
            {
                _answerProgressText.text = $"{foundAnswers.Count}/{requiredCount}";
            }
        }

        // ================ 提交结果 ================

        public void ShowSubmitResult(bool success, string answerCharacter, string message)
        {
            ShowResultTip(success, message);

            if (success)
            {
                RefreshFromGamePlay();
            }
        }

        private void ShowResultTip(bool success, string message)
        {
            if (_resultTipText == null) return;

            _resultTipText.gameObject.SetActive(true);
            _resultTipText.text = message;
            _resultTipText.color = success ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.5f, 0.3f);
            _resultTipTimer = _resultTipDuration;
            _isShowingTip = true;
        }

        private void HideResultTip()
        {
            _isShowingTip = false;
            if (_resultTipText != null)
                _resultTipText.gameObject.SetActive(false);
        }

        private void RefreshFromGamePlay()
        {
            var gamePlay = GameManager.Instance?.CurrentGamePlay;
            if (gamePlay is CorePlayGamePlay corePlay)
            {
                RefreshAnswerDisplay(corePlay.GetFoundAnswerCharacters(), corePlay.GetRequiredAnswerCount());
            }
        }

        // ================ 通关面板 ================
        

        // ================ 清除 ================

        public void ClearAll()
        {
            // if (_answerDisplayText != null) _answerDisplayText.text = "尚未找到答案";
            if (_answerProgressText != null) _answerProgressText.text = "0/0";
            HideResultTip();
        }
    }
}