using DG.Tweening;
using RTLTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    public class RewardItemWidget : UIWidget
    {
        private Image _icon;
        private RTLTextMeshPro _count;

        protected override void OnCreate()
        {
            base.OnCreate();
            _icon = FindChildComponent<Image>("RewardsIcon");
            _count = FindChildComponent<RTLTextMeshPro>("m_num");
        }

        protected override void OnClose()
        {
            DOTween.Kill(transform);
            base.OnClose();
        }

        /// <summary>设置奖励道具图标和数量</summary>
        public void SetReward(Sprite icon, int count)
        {
            // 位置由父级 LayoutGroup 管理，不能写回布局计算前的坐标。
            transform.localScale = Vector3.one;
            var canvasGroup = rectTransform.GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (_icon != null)
                _icon.color = new Color(_icon.color.r, _icon.color.g, _icon.color.b, 1f);
            if (_count != null)
                _count.color = new Color(_count.color.r, _count.color.g, _count.color.b, 1f);
            SetIcon(icon);
            if (_count != null)
                _count.text = count > 0 ? $"×{count}" : "";
        }

        /// <summary>异步图标加载完成时只更新图标，不重置布局或动画状态。</summary>
        public void SetIcon(Sprite icon)
        {
            if (_icon != null)
                _icon.sprite = icon;
        }

        /// <summary>播放入场动画：缩放弹入</summary>
        public void PlayShowAnim(float delay = 0f)
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, 0.35f)
                .SetEase(Ease.OutBack)
                .SetDelay(delay)
                .SetTarget(transform);
        }

        /// <summary>播放道具飞走动画：上移 + 渐隐</summary>
        public void PlayFlyAnim(float duration, TweenCallback onComplete)
        {
            if (rectTransform == null)
            {
                onComplete?.Invoke();
                return;
            }

            // 上移到目标位置 + 渐隐
            Sequence seq = DOTween.Sequence();
            seq.Join(rectTransform.DOAnchorPos(rectTransform.anchoredPosition + Vector2.up * 80f, duration).SetEase(Ease.InQuad));
            seq.Join(rectTransform.GetComponent<CanvasGroup>()?.DOFade(0f, duration));
            if (_icon != null)
                seq.Join(_icon.DOFade(0f, duration));
            if (_count != null)
                seq.Join(_count.DOFade(0f, duration));
            bool completed = false;
            void CompleteOnce()
            {
                if (completed) return;
                completed = true;
                onComplete?.Invoke();
            }
            seq.OnComplete(CompleteOnce);
            seq.OnKill(CompleteOnce);
            seq.SetUpdate(true);
            seq.SetTarget(rectTransform);
        }
    }
}
