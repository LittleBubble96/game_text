using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WeChatWASM;

namespace GameLogic.GamePlay.CorePlay.View
{
    public class GameViewRoot : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer backGround;
        
        [SerializeField]
        private SpriteRenderer bg;

        [SerializeField]
        private Transform characterRoot;

        [SerializeField]
        private Transform characterIkRoot;
        
        [SerializeField]
        private Transform slotRoot;

        private Sequence _rootSeq;
        
        public Transform CharacterRoot => characterRoot;

        public Transform CharacterIkRoot => characterIkRoot;

        public Transform SlotRoot => slotRoot;

        public void Init()
        {
        }

        public void OnEnterGameAnim()
        {
            BgAdapter();
            bg.color = new Color(1, 1, 1, 0);
            backGround.color = new Color(1, 1, 1, 0);
            _rootSeq?.Kill();
            _rootSeq = DOTween.Sequence();
            _rootSeq.Append(bg.DOFade(1, 0.5f));
            _rootSeq.Join(backGround.DOFade(1, 0.5f));
        }

        public void OnEndGameAnim()
        {
            _rootSeq?.Kill();
            _rootSeq = DOTween.Sequence();
            _rootSeq.Append(bg.DOFade(0, 0.5f));
            _rootSeq.Append(backGround.DOFade(0, 0.5f));
        }

        private void BgAdapter()
        {
            float scaleNormal = 1920f / 1080f;
            float currentScale = GetScreenHeight() / (float)GetScreenWidth();
            if (scaleNormal > currentScale)
            {
                backGround.transform.localScale = (scaleNormal / currentScale) * Vector3.one * 0.65f;
            }
            else
            {
                backGround.transform.localScale = (currentScale / scaleNormal) * Vector3.one * 0.65f;
            }
        }
        
        
        private int GetScreenWidth()
        {
#if UNITY_EDITOR
            return Screen.width;
#endif
            WindowInfo windowInfo = WX.GetWindowInfo();
            return (int)windowInfo.screenWidth;
        }

        private int GetScreenHeight()
        {
#if UNITY_EDITOR
            return Screen.height;
#endif
            WindowInfo windowInfo = WX.GetWindowInfo();
            return (int)windowInfo.screenHeight;
        }
    }
}