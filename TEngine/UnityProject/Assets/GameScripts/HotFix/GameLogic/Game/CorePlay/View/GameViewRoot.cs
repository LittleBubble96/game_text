using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.GamePlay.CorePlay.View
{
    public class GameViewRoot : MonoBehaviour
    {
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
            bg.color = new Color(1, 1, 1, 0);
            _rootSeq?.Kill();
            _rootSeq = DOTween.Sequence();
            _rootSeq.Append(bg.DOFade(1, 0.5f));
        }

        public void OnEndGameAnim()
        {
            _rootSeq?.Kill();
            _rootSeq = DOTween.Sequence();
            _rootSeq.Append(bg.DOFade(0, 0.5f));
        }
    }
}