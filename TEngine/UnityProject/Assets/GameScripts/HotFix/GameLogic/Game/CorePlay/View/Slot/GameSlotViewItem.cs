using DG.Tweening;
using RTLTMPro;
using UnityEngine;

namespace GameLogic.GamePlay.CorePlay.View
{
    public class GameSlotViewItem : MonoBehaviour
    {
        public const string ResPath = "GameSlotViewItem";
        
        [SerializeField] private RTLTextMeshPro3D content;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private SpriteRenderer contentBg;
        [SerializeField] private GameObject root;

        public bool IsFilled { get; private set; }

        /// <summary>显示空状态：只显示背景，不显示内容</summary>
        public void ShowEmptyState()
        {
            IsFilled = false;
            contentRoot.gameObject.SetActive(false);
            if (content != null) content.text = "";
        }

        /// <summary>设置内容并播放放入动画</summary>
        public void SetContentAndPlay(string text)
        {
            IsFilled = true;
            contentRoot.gameObject.SetActive(true);
            if (content != null) content.text = text;
            PlayPutAnimation();
        }

        /// <summary>直接设置内容（无动画，用于恢复存档）</summary>
        public void SetContentImmediate(string text)
        {
            IsFilled = true;
            contentRoot.gameObject.SetActive(true);
            if (content != null)
            {
                content.text = text;
                content.transform.localPosition = Vector3.zero;
                content.color = new Color(content.color.r, content.color.g, content.color.b, 1f);
            }
        }

        private void PlayPutAnimation()
        {
            contentRoot.transform.localPosition = new Vector3(-0.5f, -2, 0);
            content.color = new Color(content.color.r, content.color.g, content.color.b, 0);
            contentBg.color = new Color(contentBg.color.r, contentBg.color.g, contentBg.color.b, 0);
            Sequence sequence = DOTween.Sequence();
            sequence.Append(contentRoot.transform.DOLocalMove(Vector3.zero, 0.25f).SetEase(Ease.OutCubic));
            sequence.Join(content.DOColor(new Color(content.color.r, content.color.g, content.color.b, 1f), 0.2f).SetEase(Ease.OutCubic));
            sequence.Join(contentBg.DOColor(new Color(contentBg.color.r, contentBg.color.g, contentBg.color.b, 1f), 0.2f).SetEase(Ease.OutCubic));
            sequence.Append(root.transform.DOScale(0.9f, 0.1f));
            sequence.Append(root.transform.DOScale(1f, 0.05f));
        }
    }
}