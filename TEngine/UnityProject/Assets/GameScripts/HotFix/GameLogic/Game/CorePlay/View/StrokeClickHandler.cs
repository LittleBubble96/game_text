using System;
using System.Collections.Generic;
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
        /// <summary>圆形检测半径（世界坐标）。点击点周围该半径内的笔画都会被纳入候选。</summary>
        [SerializeField] private float _detectRadius = 0.1f;

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

        /// <summary>
        /// 2D 圆形范围检测：以点击点为圆心、<see cref="_detectRadius"/> 为半径，
        /// 取圆内所有笔画 Collider，命中多个时优先选择离点击点最近的。
        /// </summary>
        private int? RaycastStroke2D(Vector3 screenPos)
        {
            Vector2 worldPos = _rayCamera.ScreenToWorldPoint(screenPos);
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, _detectRadius, _strokeLayerMask);
            if (hits == null || hits.Length == 0) return null;

            int bestIndex = -1;
            float bestSqrDist = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;

                // 笔画名为 "Stroke_0", "Stroke_1" ...
                string name = hit.gameObject.name;
                if (!name.StartsWith("Stroke_")) continue;
                if (!int.TryParse(name.Substring(7), out int index)) continue;

                // 用 AABB 最近点到点击点的距离作为"就近"度量。
                // 手算 Clamp（不依赖 Bounds.ClosestPoint），全平台/IL2CPP 安全
                Bounds b = hit.bounds;
                float closestX = Mathf.Clamp(worldPos.x, b.min.x, b.max.x);
                float closestY = Mathf.Clamp(worldPos.y, b.min.y, b.max.y);
                float dx = closestX - worldPos.x;
                float dy = closestY - worldPos.y;
                float sqr = dx * dx + dy * dy;
                if (sqr < bestSqrDist)
                {
                    bestSqrDist = sqr;
                    bestIndex = index;
                }
            }

            return bestIndex >= 0 ? bestIndex : (int?)null;
        }

        // 编辑器可视化检测范围，便于调参
        private void OnDrawGizmosSelected()
        {
            if (_rayCamera == null) _rayCamera = Camera.main;
            if (_rayCamera == null) return;

            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Vector3 center = _rayCamera.ScreenToWorldPoint(Input.mousePosition);
            center.z = 0f;
            Gizmos.DrawWireSphere(center, _detectRadius);
        }
    }
}
