using DG.Tweening;
using UnityEngine;

namespace GameLogic.UI
{
    public class UIHomeTabContentWidget : UIWidget
    {
        private RectTransform _parentTf;
        
        public virtual void OnInit(RectTransform parent)
        {
            _parentTf = parent;
        }

        public void OnEnter(bool immediate , bool isRight)
        {
            Visible = true;
            if (immediate)
            {
                transform.localPosition = Vector3.zero;
                transform.localScale = Vector3.one;
            }
            else
            {
                rectTransform.anchoredPosition = new Vector2((isRight ? -1 : 1) * GenerateStartAncX(), 0);
                rectTransform.DOLocalMoveX(0, 0.2f).SetEase(Ease.OutBack);
            }
        }
        
        public void OnExit(bool isRight)
        {
            rectTransform.DOLocalMoveX((isRight ? 1 : -1) * _parentTf.rect.width, 0.2f).
                SetEase(Ease.Linear).
                OnComplete(() => { Visible = false; });
        }

        private float GenerateStartAncX()
        {
            if (_parentTf)
            {
                return _parentTf.rect.width;
            }
            return 0;
        }
    }
}