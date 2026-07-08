using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.System, location: "UITop")]
    public class UITop : UIWindow
    {
        #region 脚本工具生成的代码

        private RectTransform _coinTf;
        private RectTransform _backTf;
        private CanvasGroup _coinCanvasGroup;
        private CanvasGroup _backCanvasGroup;
        private TMP_Text _coinCountText;
        private XYButton _backBtn;

        /// <summary>当前数据缓存，用于动画状态对比</summary>
        private UITopData _currentData;

        /// <summary>上个显示状态（用于判断动画方向）</summary>
        private bool _prevCoinVisible;
        private bool _prevBackVisible;

        /// <summary>动画时长</summary>
        private const float AnimDuration = 0.25f;

        /// <summary>起始偏移（屏幕上方，用于滑入）</summary>
        private const float SlideOffsetY = 80f;

        protected override void ScriptGenerator()
        {
            _coinTf = FindChildComponent<RectTransform>("CoinSlot");
            _backTf = FindChildComponent<RectTransform>("BackSlot");

            // 获取或添加 CanvasGroup（用于淡入淡出）
            if (_coinTf != null)
            {
                _coinCanvasGroup = _coinTf.GetComponent<CanvasGroup>();
                if (_coinCanvasGroup == null)
                    _coinCanvasGroup = _coinTf.gameObject.AddComponent<CanvasGroup>();
                _coinCountText = FindChildComponent<TMP_Text>("CoinSlot/root/Coin/Text (TMP)");
            }

            if (_backTf != null)
            {
                _backCanvasGroup = _backTf.GetComponent<CanvasGroup>();
                if (_backCanvasGroup == null)
                    _backCanvasGroup = _backTf.gameObject.AddComponent<CanvasGroup>();
                _backBtn = CreateWidget<XYButton>(_backTf.gameObject);
                _backBtn?.OnAddListener(OnBackClick);
            }

            // 初始状态：全部隐藏
            _prevCoinVisible = false;
            _prevBackVisible = false;
            _currentData = default;

            SetImmediateHidden(_coinTf, _coinCanvasGroup);
            SetImmediateHidden(_backTf, _backCanvasGroup);
        }

        protected override void RegisterEvent()
        {
            base.RegisterEvent();
            AddUIEvent<UITopData>(EventDefine.Event_UITopUpdate, OnTopDataUpdate);
            AddUIEvent<int>(EventDefine.Event_UITopCoinUpdate, OnCoinCountUpdate);
        }

        #endregion

        #region 事件处理

        private void OnBackClick()
        {
            GameManager.Instance.ReturnToHome();
        }

        /// <summary>
        /// 收到 UITopData 更新事件
        /// </summary>
        private void OnTopDataUpdate(UITopData newData)
        {
            // 如果数据相同，不做动画
            if (_currentData.Equals(newData))
                return;

            // 金币区域显隐
            if (newData.ShowCoin != _prevCoinVisible)
            {
                if (newData.ShowCoin)
                    PlayShowAnim(_coinTf, _coinCanvasGroup);
                else
                    PlayHideAnim(_coinTf, _coinCanvasGroup);
                _prevCoinVisible = newData.ShowCoin;
            }

            // 返回按钮显隐
            if (newData.ShowBack != _prevBackVisible)
            {
                if (newData.ShowBack)
                    PlayShowAnim(_backTf, _backCanvasGroup);
                else
                    PlayHideAnim(_backTf, _backCanvasGroup);
                _prevBackVisible = newData.ShowBack;
            }

            _currentData = newData;
        }

        /// <summary>
        /// 收到金币数量更新事件
        /// </summary>
        private void OnCoinCountUpdate(int coinCount)
        {
            if (_coinCountText != null)
            {
                _coinCountText.text = coinCount.ToString();
            }
        }

        #endregion

        #region 动画

        /// <summary>从上到下位移 + 渐显</summary>
        private void PlayShowAnim(RectTransform target, CanvasGroup canvasGroup)
        {
            if (target == null) return;

            // 终止之前动画
            DOTween.Kill(target);
            if (canvasGroup != null) DOTween.Kill(canvasGroup);

            // 设置起始状态：上方偏移 + 透明
            target.anchoredPosition = new Vector2(target.anchoredPosition.x, SlideOffsetY);
            target.gameObject.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            // 滑入中心 + 淡入
            target.DOAnchorPosY(0f, AnimDuration).SetEase(Ease.OutCubic).SetTarget(target);
            if (canvasGroup != null)
                canvasGroup.DOFade(1f, AnimDuration).SetEase(Ease.OutCubic).SetTarget(canvasGroup);
        }

        /// <summary>从下到上位移 + 渐隐</summary>
        private void PlayHideAnim(RectTransform target, CanvasGroup canvasGroup)
        {
            if (target == null) return;

            // 终止之前动画
            DOTween.Kill(target);
            if (canvasGroup != null) DOTween.Kill(canvasGroup);

            // 滑出上方 + 淡出
            target.DOAnchorPosY(SlideOffsetY, AnimDuration).SetEase(Ease.InCubic).SetTarget(target);
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, AnimDuration)
                    .SetEase(Ease.InCubic)
                    .OnComplete(() => target.gameObject.SetActive(false))
                    .SetTarget(canvasGroup);
            }
            else
            {
                // 没有 CanvasGroup 时延迟隐藏
                DOTween.To(() => 0f, _ => { }, 0f, AnimDuration)
                    .OnComplete(() => target.gameObject.SetActive(false))
                    .SetTarget(target);
            }
        }

        /// <summary>立即隐藏，无动画（初始化用）</summary>
        private void SetImmediateHidden(RectTransform target, CanvasGroup canvasGroup)
        {
            if (target == null) return;
            target.anchoredPosition = new Vector2(target.anchoredPosition.x, SlideOffsetY);
            target.gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        #endregion

        #region 生命周期

        protected override void OnDestroy()
        {
            // 清理 DOTween
            if (_coinTf != null) DOTween.Kill(_coinTf);
            if (_backTf != null) DOTween.Kill(_backTf);
            if (_coinCanvasGroup != null) DOTween.Kill(_coinCanvasGroup);
            if (_backCanvasGroup != null) DOTween.Kill(_backCanvasGroup);

            base.OnDestroy();
        }

        #endregion
    }
}
