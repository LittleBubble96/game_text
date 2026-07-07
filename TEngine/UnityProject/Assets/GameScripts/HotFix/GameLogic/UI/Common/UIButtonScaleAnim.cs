using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic.Game.UI
{
    public class UIButtonScaleAnim : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        protected static bool _confInitialized;
        
        [SerializeField]
        protected Transform _animTrans;
        protected Vector3 _originScale;
        protected Vector3 _animScale;
        protected Button _button;

        protected bool _hasBtn;
        protected bool _isPressing;
        protected Tweener _scaleTweener;
        
        private static float _scaleX;
        private static float _scaleY;
        
        protected virtual Vector2 GetTargetScale()
        {
            if (!_confInitialized)
            {
                _confInitialized = true;
                float confScale = 75 / 100f;
                _scaleX = confScale;
                _scaleY = confScale;
            }

            return new Vector2(_scaleX, _scaleY);
        }

        protected virtual void OnEnable()
        {
            if (!_animTrans)
            {
                _animTrans = transform;
            }

            _originScale = _animTrans.localScale;
            if (_button == null)
            {
                _button = GetComponent<Button>();
                _hasBtn = _button != null;
            }
        }

        protected virtual void OnDisable()
        {
            _scaleTweener?.Kill();
            _animTrans.localScale = _originScale;
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            Vector2 confScale = GetTargetScale();
            _animScale = new Vector3(_originScale.x * confScale.x, _originScale.y * confScale.y, _originScale.z);

            if (_button.enabled == false)
            {
                _scaleTweener.Kill();
                return;
            }

            if (!_hasBtn || _button.interactable)
            {
                _scaleTweener?.Kill();
                _scaleTweener = _animTrans.DOScale(_animScale, 0.1f);
                _isPressing = true;
            }
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (_button.enabled == false)
            {
                _scaleTweener.Kill();
                return;
            }

            ResumeAnim();
        }

        private void ResumeAnim()
        {
            if (!_hasBtn || _button.interactable)
            {
                _scaleTweener?.Kill();
                _scaleTweener = _animTrans.DOScale(_originScale, 0.07f).SetEase(Ease.OutBack);
            }

            _isPressing = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isPressing)
            {
                ResumeAnim();
            }
        }
    }
}