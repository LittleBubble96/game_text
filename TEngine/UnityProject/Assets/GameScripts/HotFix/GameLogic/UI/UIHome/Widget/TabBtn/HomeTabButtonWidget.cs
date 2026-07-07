using System;
using DG.Tweening;
using UnityEngine.UI;

namespace GameLogic
{
    public class HomeTabButtonWidget : UIWidget
    {
        private const float SelectFlexibleWidth = 1.2f;
        private const float UnSelectFlexibleWidth = 1f;

        private LayoutElement _layoutElement;
        private XYButton _button;
        
        private ETabType _index;
        private bool _isSelect = false;
        private Action<ETabType> _onClick;

        protected override void OnCreate()
        {
            base.OnCreate();
            _layoutElement = transform.GetComponent<LayoutElement>();
            _button = CreateWidget<XYButton>("btn");
            _button.OnAddListener(OnClickBtn);
        }

        private void OnClickBtn()
        {
            _onClick?.Invoke(_index);
        }

        public void Init(ETabType index , Action<ETabType> onClick)
        {
            _index = index;
            _onClick = onClick;
        }

        public void DoSelect()
        {
            if (_isSelect)
            {
                return;
            }
            _isSelect = true;

            DOTween.To(() => _layoutElement.flexibleWidth, (t) =>
            {
                _layoutElement.flexibleWidth = t;
            }, SelectFlexibleWidth, 0.18f);
        }

        public void DoUnSelect()
        {
            if (!_isSelect)
            {
                return;
            }
            DOTween.To(() => _layoutElement.flexibleWidth, (t) =>
            {
                _layoutElement.flexibleWidth = t;
            }, UnSelectFlexibleWidth, 0.18f);
            _isSelect = false;
        }
    }
}