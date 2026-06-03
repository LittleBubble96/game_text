using System.Collections.Generic;
using GameLogic.GamePlay;
using GameLogic.GamePlay.CorePlay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// CorePlay 的 Canvas UI 管理 —— 通过 IGamePlay 接口获取数据，不依赖具体实现
    /// </summary>
    public class UICorePlay : UIWindow
    {
        private TMP_Text _levelNameText;

        private TMP_Text _answerDisplayText;
        private TMP_Text _answerProgressText;

        private XYButton _submitButton;

        private TMP_Text _resultTipText;

        private float _resultTipDuration = 2f;

        private float _resultTipTimer;
        private bool _isShowingTip;
        
        private CorePlayGamePlay CorePlayGamePlay => GameManager.Instance?.CurrentGamePlay as CorePlayGamePlay;

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _levelNameText = FindChildComponent<TMP_Text>("Titile");
            _answerDisplayText = FindChildComponent<TMP_Text>("Answer");
            _answerProgressText = FindChildComponent<TMP_Text>("AnswerProgress");
            _submitButton = CreateWidget<XYButton>("SubmitBtn");
            _resultTipText = FindChildComponent<TMP_Text>("ResultTip");
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
            if (_resultTipText != null)
                _resultTipText.gameObject.SetActive(false);
            SetLevelName( GameManager.Instance.CurrentGamePlay.GetLevelName());
            // 更新 UI
            if (GameManager.Instance.CurrentGamePlay is CorePlayGamePlay corePlay)
            {
                RefreshAnswerDisplay(corePlay.GetFoundAnswerCharacters(), corePlay.GetRequiredAnswerCount());
            }
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

        private void OnAnswerSubmitted(bool success, string answerCharacter, string message)
        {
            ShowSubmitResult(success, answerCharacter, message);
        }

        private void SetLevelName(string name)
        {
            if (_levelNameText != null)
                _levelNameText.text = name;
        }

        // ================ 答案显示 ================

        public void RefreshAnswerDisplay(List<string> foundAnswers, int requiredCount)
        {
            if (_answerDisplayText != null)
            {
                _answerDisplayText.text = foundAnswers.Count > 0
                    ? "已找到: " + string.Join("  ", foundAnswers)
                    : "尚未找到答案";
            }

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
            if (_answerDisplayText != null) _answerDisplayText.text = "尚未找到答案";
            if (_answerProgressText != null) _answerProgressText.text = "0/0";
            HideResultTip();
        }
    }
}