using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    public class GmBtnAndInputItemWidget : UIWidget
    {
        private XYButton _button;
        private TMP_InputField _inputField;
        private Text _labelText;

        private Action<string> _onClickHasInput;
        private Action _onClick;

        private GmItemType _gmItemType;

        protected override void OnCreate()
        {
            base.OnCreate();
            _button = CreateWidget<XYButton>("Button");
            _button.OnAddListener(OnClick);
            _inputField = FindChildComponent<TMP_InputField>("InputField");
            _labelText = FindChildComponent<Text>("Button/Text");
        }

        private void OnClick()
        {
            if (_gmItemType == GmItemType.BtnAndInput)
            {
                _onClickHasInput?.Invoke(_inputField != null ? _inputField.text : "");
            }
            else
            {
                _onClick?.Invoke();
            }
        }

        /// <summary>初始化带输入框的按钮</summary>
        public void InitBtnAndInput(string label, Action<string> callback)
        {
            _onClickHasInput = callback;
            _gmItemType = GmItemType.BtnAndInput;
            SetLabel(label);
            if (_inputField != null)
                _inputField.gameObject.SetActive(true);
        }

        /// <summary>初始化纯按钮</summary>
        public void InitBtn(string label, Action callback)
        {
            _onClick = callback;
            _gmItemType = GmItemType.Btn;
            SetLabel(label);
            if (_inputField != null)
                _inputField.gameObject.SetActive(false);
        }

        private void SetLabel(string label)
        {
            if (_labelText != null)
                _labelText.text = label;
        }

        enum GmItemType
        {
            BtnAndInput,
            Btn,
        }
    }
}