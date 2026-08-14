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
        /// 2D 笔画检测：先点检测，命中即返回；未命中再圆形兜底取最近的笔画。
        /// </summary>
        /// <remarks>
        /// 笔画使用 PolygonCollider2D，其 bounds（AABB）对细长笔画会明显偏大，
        /// 用 AABB 最近点做就近度量会误判（点未落在某笔画上，却因 AABB 更近而被选中）。
        /// 故分两步：
        /// 1) 点检测：<see cref="Physics2D.OverlapPoint"/> 直接命中笔画多边形即返回，精确无歧义；
        /// 2) 圆形兜底：未命中时在 <see cref="_detectRadius"/> 内取候选，用
        ///    <see cref="Collider2D.Distance"/> 的真实最近距离选最近，规避 AABB 误差。
        /// </remarks>
        private int? RaycastStroke2D(Vector3 screenPos)
        {
            Vector2 worldPos = _rayCamera.ScreenToWorldPoint(screenPos);

            // 1) 点检测：点击点直接落在某笔画多边形内，立即返回
            Collider2D pointHit = Physics2D.OverlapPoint(worldPos, _strokeLayerMask);
            int? directIndex = GetStrokeIndex(pointHit);
            if (directIndex.HasValue) return directIndex;

            // 2) 圆形兜底：取半径内所有候选，按真实最近距离选最近的
            Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, _detectRadius, _strokeLayerMask);
            if (hits == null || hits.Length == 0) return null;

            int bestIndex = -1;
            float bestSqrDist = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                int? index = GetStrokeIndex(hit);
                if (!index.HasValue) continue;

                // 用 Collider2D.ClosestPoint 取多边形表面最近点，再算到点击点的距离。
                // 比 AABB（hit.bounds）最近点更贴合细长笔画的实际形状，避免大包围盒误判。
                Vector2 closest = hit.ClosestPoint(worldPos);
                float sqr = (closest - worldPos).sqrMagnitude;
                if (sqr < bestSqrDist)
                {
                    bestSqrDist = sqr;
                    bestIndex = index.Value;
                }
            }

            return bestIndex >= 0 ? bestIndex : (int?)null;
        }

        /// <summary>
        /// 从 Collider 解析笔画索引。笔画物体名为 "Stroke_0", "Stroke_1" ...，
        /// 非笔画或解析失败返回 null。
        /// </summary>
        private static int? GetStrokeIndex(Collider2D hit)
        {
            if (hit == null) return null;
            string name = hit.gameObject.name;
            if (!name.StartsWith("Stroke_")) return null;
            return int.TryParse(name.Substring(7), out int index) ? index : (int?)null;
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
