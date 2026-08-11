using System.Runtime.InteropServices;
using TEngine;
using UnityEngine;
using WeChatWASM;

namespace GameLogic.Platform.Adapter
{
    /// <summary>
    /// 安全区适配组件。挂到任意 RectTransform 上，按屏幕安全区自动收边，
    /// 主要控制 <see cref="RectTransform.offsetMax"/>（顶部 / 右侧），把内容从
    /// 刘海 / 状态栏 / Home Indicator / 圆角区域收回安全区内。
    /// </summary>
    /// <remarks>
    /// 与框架 <see cref="SetUISafeFitHelper"/> 的区别：后者由 UIWindow 驱动、需显式调用；
    /// 本组件直接挂节点即生效，OnEnable / 屏幕变更 / 分辨率变更时自动重算，适合挂在
    /// UIRoot 下的固定容器或需要跟随安全区动态收边的子节点上。
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OffsetMaxAdapter : MonoBehaviour
    {
        public enum FitSide
        {
            None = 0,
            Top = 1,
            Bottom = 2,
            Left = 4,
            Right = 8,
            All = Top | Bottom | Left | Right,
        }

        [Header("适配范围")]
        [Tooltip("控制哪些边收进安全区。默认仅顶部（最常见：避开状态栏/刘海）。")]
        [SerializeField] private FitSide _fitSides = FitSide.Top;

        [Header("额外留白（像素，叠加到安全区偏移之上）")]
        [Tooltip("顶部额外留白（offsetMax.y 会多收这么多）")]
        [SerializeField] private float _topPadding = 0f;
        [Tooltip("右侧额外留白（offsetMax.x 会多收这么多）")]
        [SerializeField] private float _rightPadding = 0f;
        [Tooltip("底部额外留白（offsetMin.y 会多收这么多）")]
        [SerializeField] private float _bottomPadding = 0f;
        [Tooltip("左侧额外留白（offsetMin.x 会多收这么多）")]
        [SerializeField] private float _leftPadding = 0f;

        /// <summary>是否使用挖孔屏（DisplayCutout）信息，Android 刘海机更精确。</summary>
        [Header("高级")]
        [SerializeField] private bool _useCutout = true;

        private RectTransform _rect;
        

        /// <summary>防重入守卫：Apply 改 offset 会触发 OnRectTransformDimensionsChange，
        /// 不加守卫会形成“改 offset → 回调 → 又改 offset”的死循环。</summary>
        private bool _isApplying;

        private static readonly Vector2 ZeroOffset = Vector2.zero;

        private RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = GetComponent<RectTransform>();
                }
                return _rect;
            }
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            // Apply 改 offset 会再次触发本回调，由 Apply 内部守卫拦截，避免死循环
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            Apply();
        }
#endif

        /// <summary>立即按当前安全区重算 offset。</summary>
        public void Apply()
        {
            // 防重入：本方法会写 offsetMax/offsetMin，触发 OnRectTransformDimensionsChange 再次进入 Apply
            if (_isApplying) return;
            RectTransform rt = Rect;
            if (rt == null) return;

            _isApplying = true;
            try
            {
                ApplyInternal(rt);
            }
            finally
            {
                _isApplying = false;
            }
        }

        private void ApplyInternal(RectTransform rt)
        {
            Rect safe = GetSafeArea();
            int sw = GetScreenWidth();
            int sh = GetScreenHeight();

            // 安全区无效（极早期/无显示）时退回零偏移，避免误伤全屏内容
            if (safe.width <= 0 || safe.height <= 0 || sw <= 0 || sh <= 0)
            {
                Log.Info($"OffsetMaxAdapter.Apply: safeArea={safe}, screen=({sw},{sh}), reset offset to zero");
                SetOffsets(rt, ZeroOffset, ZeroOffset);
                return;
            }

            // offsetMax：右上角向“内”收（x 为负、y 为负）
            // 安全区右边界到屏幕右边界 = 需要收的右边距
            float rightInset = _useCutout ? GetRightInset(safe, sw) : (sw - safe.xMax);
            float topInset = _useCutout ? GetTopInset(safe, sh) : (sh - safe.yMax);

            float offsetXMax = 0f;
            float offsetYMax = 0f;
            if ((_fitSides & FitSide.Right) != 0)
            {
                offsetXMax = -(rightInset + _rightPadding);
            }
            if ((_fitSides & FitSide.Top) != 0)
            {
                offsetYMax = -(topInset + _topPadding);
            }

            // offsetMin：左下角向“内”收（x 为正、y 为正）
            float leftInset = _useCutout ? GetLeftInset(safe) : safe.xMin;
            float bottomInset = _useCutout ? GetBottomInset(safe) : safe.yMin;

            float offsetXMin = 0f;
            float offsetYMin = 0f;
            if ((_fitSides & FitSide.Left) != 0)
            {
                offsetXMin = leftInset + _leftPadding;
            }
            if ((_fitSides & FitSide.Bottom) != 0)
            {
                offsetYMin = bottomInset + _bottomPadding;
            }

            SetOffsets(rt, new Vector2(offsetXMax, offsetYMax), new Vector2(offsetXMin, offsetYMin));
            Log.Info($"OffsetMaxAdapter.Apply: safeArea={safe}, screen=({sw},{sh}), offsetMax=({offsetXMax},{offsetYMax}), offsetMin=({offsetXMin},{offsetYMin})");
        }

        /// <summary>仅在值真的变化时写 offset，减少 OnRectTransformDimensionsChange 的级联触发</summary>
        private static void SetOffsets(RectTransform rt, Vector2 max, Vector2 min)
        {
            if (rt.offsetMax != max)
            {
                rt.offsetMax = max;
            }
            if (rt.offsetMin != min)
            {
                rt.offsetMin = min;
            }
        }

        // ================ 安全区 inset 计算 ================

        /// <summary>顶部需要收的像素 = 屏幕高度 - 安全区顶部</summary>
        private float GetTopInset(Rect safe, int screenH)
        {
            float inset = screenH - safe.yMax;
            // 挖孔屏（Android 刘海等）取最大挖孔高度，避免状态栏 + 刘海叠加遗漏
            if (_useCutout)
            {
                Rect[] cutouts = Screen.cutouts;
                if (cutouts != null)
                {
                    for (int i = 0; i < cutouts.Length; i++)
                    {
                        float cTop = cutouts[i].yMax;
                        if (cTop > inset) inset = cTop;
                    }
                }
            }
            return Mathf.Max(0f, inset);
        }

        /// <summary>右侧需要收的像素 = 屏幕宽度 - 安全区右边界</summary>
        private float GetRightInset(Rect safe, int screenW)
        {
            float inset = screenW - safe.xMax;
            return Mathf.Max(0f, inset);
        }

        /// <summary>左侧需要收的像素 = 安全区左边界</summary>
        private float GetLeftInset(Rect safe)
        {
            return Mathf.Max(0f, safe.xMin);
        }

        /// <summary>底部需要收的像素 = 安全区底部</summary>
        private float GetBottomInset(Rect safe)
        {
            return Mathf.Max(0f, safe.yMin);
        }

        public Rect GetSafeArea()
        {
#if UNITY_EDITOR
            Rect safe = Screen.safeArea;
            Log.Info($"OffsetMaxAdapter.GetSafeArea UNITY_EDITOR: safeArea={safe}, screen=({Screen.width},{Screen.height})");
            return safe;
#endif
            WindowInfo windowInfo = WX.GetWindowInfo();
            SafeArea safeArea = windowInfo.safeArea;
            Rect safeWx = new Rect(0, 0, (float)safeArea.width, (float)safeArea.height);
            Log.Info($"OffsetMaxAdapter.GetSafeArea WX: safeArea={safeWx}, screen=({safeArea.width},{safeArea.height}) " +
                     $"windowWH:{windowInfo.windowWidth},{windowInfo.windowHeight} , ScreenWH:{windowInfo.screenWidth} ,{windowInfo.screenHeight}");
            return safeWx;
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
