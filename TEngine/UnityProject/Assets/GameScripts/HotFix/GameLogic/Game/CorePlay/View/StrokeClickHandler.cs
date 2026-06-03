using System;
using GameLogic.View;
using UnityEngine;

namespace GameLogic.GamePlay.CorePlay.View
{
    /// <summary>
    /// 笔画点击处理器 —— 通过 2D 射线检测统一处理
    /// 编辑器用鼠标，移动端用 Touch
    /// </summary>
    public class StrokeInputHandler : MonoBehaviour
    {
        [Header("射线检测")]
        [SerializeField] private Camera _rayCamera;
        [SerializeField] private LayerMask _strokeLayerMask = ~0;

        private Action<int> _onStrokeClicked;
        private DrawCharacter _drawCharacter;

        public void Initialize(DrawCharacter drawCharacter, Camera rayCamera, Action<int> onStrokeClicked)
        {
            _drawCharacter = drawCharacter;
            _rayCamera = rayCamera;
            _onStrokeClicked = onStrokeClicked;
        }

        private void Update()
        {
            if (_drawCharacter == null || _rayCamera == null) return;

            bool isPressed = false;
            Vector3 screenPosition = Vector3.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
            isPressed = Input.GetMouseButtonDown(0);
            screenPosition = Input.mousePosition;
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                isPressed = touch.phase == TouchPhase.Began;
                screenPosition = touch.position;
            }
#endif
            if (!isPressed) return;

            int? hitStrokeIndex = RaycastStroke2D(screenPosition);
            if (hitStrokeIndex.HasValue)
            {
                _onStrokeClicked?.Invoke(hitStrokeIndex.Value);
            }
        }

        /// <summary>2D 射线检测：将屏幕坐标转到世界坐标，用 OverlapPoint 命中笔画 Collider2D</summary>
        private int? RaycastStroke2D(Vector3 screenPos)
        {
            Vector2 worldPos = _rayCamera.ScreenToWorldPoint(screenPos);
            Collider2D hit = Physics2D.OverlapPoint(worldPos, _strokeLayerMask);

            if (hit != null)
            {
                // 笔画名为 "Stroke_0", "Stroke_1" ...
                string name = hit.gameObject.name;
                if (name.StartsWith("Stroke_"))
                {
                    if (int.TryParse(name.Substring(7), out int index))
                    {
                        return index;
                    }
                }
            }
            return null;
        }
    }
}