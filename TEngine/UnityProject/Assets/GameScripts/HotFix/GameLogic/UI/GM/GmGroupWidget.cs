using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    public class GmTopBtnWidget : UIWidget
    {
        private XYButton _button;
        private Text _title;

        protected override void OnCreate()
        {
            base.OnCreate();
            _title = FindChildComponent<Text>("Text");
            _button = CreateWidget<XYButton>("");
            _button.OnAddListener(OnClick);
        }

        public void Init(string title, Action<string> onClick)
        {
            _title.text = title;
            _onClick = onClick;
        }

        private Action<string> _onClick;

        private void OnClick()
        {
            _onClick?.Invoke(_title.text);
        }
    }

    public class GmGroupWidget : UIWidget
    {
        private const string BtnAndInputRes = "BtnAndInput_GM";

        private RectTransform _content;
        private Text _title;

        protected override void OnCreate()
        {
            base.OnCreate();
            _content = FindChildComponent<RectTransform>("Content");
            _title = FindChildComponent<Text>("Title/Image/Text");
        }

        /// <summary>设置组标题</summary>
        public void SetTitle(string title)
        {
            if (_title != null)
                _title.text = title;
        }

        /// <summary>创建一个纯按钮 GM 项</summary>
        public void CreateBtn(string label, Action action)
        {
            var widget = CreateWidgetByPath<GmBtnAndInputItemWidget>(_content, BtnAndInputRes);
            widget.InitBtn(label, action);
        }

        /// <summary>创建一个带输入框的按钮 GM 项</summary>
        public void CreateBtnAndInput(string label, Action<string> action)
        {
            var widget = CreateWidgetByPath<GmBtnAndInputItemWidget>(_content, BtnAndInputRes);
            widget.InitBtnAndInput(label, action);
        }
    }
}