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
        

        private float _resultTipDuration = 1f;

        private Transform _toastRoot;
        private RTLTextMeshPro _toastTmp;
        private float _resultTipTimer;
        private bool _isShowingTip;
        
        private CorePlayLayoutWidget _layoutWidget;
        
        private CorePlayPropWidget _tipsPropWidget;

        private CorePlayPropWidget _resetPropWidget;
        

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _levelNameText = FindChildComponent<RTLTextMeshPro>("Panel/TitleRoot/Titile");
            // _answerDisplayText = FindChildComponent<RTLTextMeshPro>("Answer");
            _answerProgressText = FindChildComponent<RTLTextMeshPro>("Panel/AnswerProgress");
            _submitBtnText = FindChildComponent<RTLTextMeshPro>("Panel/Buttom/SubmitBtn/m_text");
            _submitButton = CreateWidget<XYButton>("Panel/Buttom/SubmitBtn");
            _toastRoot = FindChildComponent<Transform>("Panel/Toast");
            _toastTmp = FindChildComponent<RTLTextMeshPro>("Panel/Toast/UIToastEffect/bg/m_text");
            _layoutWidget = CreateWidget<CorePlayLayoutWidget>("Panel/Layout");
            _tipsPropWidget = CreateWidget<CorePlayPropWidget>("Panel/Buttom/Props/TipProp");
            _tipsPropWidget.OnInit(PropType.Tip);
            _resetPropWidget = CreateWidget<CorePlayPropWidget>("Panel/Buttom/Props/ResetProp");
            _resetPropWidget.OnInit(PropType.Reset);
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
            AddUIEvent(EventDefine.Event_PropResetDone, OnPropResetDone);
        }
        
        protected override void OnRefresh()
        {
            base.OnRefresh();
            _tipsPropWidget.Refresh();
            _resetPropWidget.Refresh();
            _toastRoot.gameObject.SetActive(false);

            GameEvent.Send(EventDefine.Event_UITopUpdate, new UITopData(showCoin: true, showBack: true));
            GameEvent.Send(EventDefine.Event_UITopCoinUpdate, PropDefine.CoinCount);
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

        /// <summary>重置道具使用完成：刷新进度文字（答案已清空 → 0/N）</summary>
        private void OnPropResetDone()
        {
            if (GameManager.Instance?.CurrentGamePlay is CorePlayGamePlay corePlay)
            {
                RefreshAnswerDisplay(corePlay.GetFoundAnswerCharacters(), corePlay.GetRequiredAnswerCount());
            }
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
            if (_toastRoot == null || success) return;
            _toastRoot.gameObject.SetActive(false);
            _toastRoot.gameObject.SetActive(true);
            _toastTmp.text = message;
            _resultTipTimer = _resultTipDuration;
            _isShowingTip = true;
        }

        private void HideResultTip()
        {
            _isShowingTip = false;
            _toastRoot.gameObject.SetActive(false);
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