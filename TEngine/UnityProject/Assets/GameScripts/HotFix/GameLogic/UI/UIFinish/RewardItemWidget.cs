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

        /// <summary>设置奖励道具图标和数量</summary>
        public void SetReward(Sprite icon, int count)
        {
            if (_icon != null)
                _icon.sprite = icon;
            if (_count != null)
                _count.text = count > 0 ? $"×{count}" : "";
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
            if (rectTransform == null) return;

            // 上移到目标位置 + 渐隐
            Sequence seq = DOTween.Sequence();
            seq.Join(rectTransform.DOAnchorPos(rectTransform.anchoredPosition + Vector2.up * 80f, duration).SetEase(Ease.InQuad));
            seq.Join(rectTransform.GetComponent<CanvasGroup>()?.DOFade(0f, duration));
            if (_icon != null)
                seq.Join(_icon.DOFade(0f, duration));
            if (_count != null)
                seq.Join(_count.DOFade(0f, duration));
            seq.OnComplete(onComplete);
            seq.SetTarget(rectTransform);
        }
    }
}