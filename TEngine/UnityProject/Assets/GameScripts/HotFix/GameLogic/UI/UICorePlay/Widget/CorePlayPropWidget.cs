using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameLogic.GamePlay.CorePlay;
using TEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Ease = DG.Tweening.Ease;

namespace GameLogic
{
    /// <summary>
    /// CorePlay 道具按钮 Widget —— 支持 Tip（提示）/ Reset（免费全选或清空）/ Next（下一关）等道具类型。
    /// 使用优先级（三档状态机，刷新与点击共用同一判定）：
    ///   1) 道具数量 > 0          → 显示数量节点，隐藏金币/广告节点，点击扣数量
    ///   2) 数量为 0 且金币足够   → 隐藏数量节点，显示金币节点，点击扣金币直接使用
    ///   3) 数量为 0、金币不足、有广告 → 隐藏数量/金币，显示广告，点击看激励广告
    ///   4) 数量为 0、金币不足、无广告 → 全部隐藏，按钮置灰
    /// 广告接入见 <see cref="AdSystem"/>（当前占位返回 false）。
    /// </summary>
    public class CorePlayPropWidget : UIWidget
    {
        private PropType _propType = PropType.Tip;

        private XYButton _propButton;
        private Transform _countRoot;          // 数量节点
        private TMP_Text _countText;          // 数量节点文字
        private Image _backgroundImage;       // 道具背景（置灰用）
        private Image _iconImage;             // 道具图标（置灰用）

        // 金币替代使用节点（数量为 0 且金币足够时显示）
        private Transform _coinRoot;
        private TMP_Text _coinText;

        // 广告节点（数量与金币都不可用时显示，未来扩展）
        private Transform _adRoot;

        private CorePlayGamePlay _corePlayGamePlay;
        private const string SelectAllIconPath = "Common_prop_all_select";
        private const string ClearIconPath = "Common_prop_clear";
        private Sprite _selectAllIcon;
        private Sprite _clearIcon;
        private Sprite _resetIconTarget;
        private Vector3 _iconOriginalScale = Vector3.one;
        private Sequence _iconSwitchTween;
        private bool _resetIconsLoading;
        private bool _isDestroyed;

        /// <summary>该道具的金币消耗量</summary>
        private int CoinCost => _propType switch
        {
            PropType.Tip => GameDefine.PropTipCoinCost,
            PropType.Next => GameDefine.PropNextCoinCost,
            _ => int.MaxValue,
        };

        protected override void OnCreate()
        {
            base.OnCreate();
            _propButton = CreateWidget<XYButton>("");
            _countRoot = FindChildComponent<Transform>("CountBg");
            _countText = FindChildComponent<TMP_Text>("CountBg/Count");
            _backgroundImage = FindChildComponent<Image>("bg");
            _iconImage = FindChildComponent<Image>("icon");
            if (_iconImage != null) _iconOriginalScale = _iconImage.transform.localScale;

            _coinRoot = FindChildComponent<Transform>("CoinRoot");
            _coinText = FindChildComponent<TMP_Text>("CoinRoot/CoinText");

            _adRoot = FindChildComponent<Transform>("AdRoot");

            _propButton.OnAddListener(OnPropClicked);
        }

        public void OnInit(PropType propType)
        {
            _propType = propType;
            if (_propType == PropType.Reset) LoadResetIconsAsync().Forget();
        }

        protected override void RegisterEvent()
        {
            base.RegisterEvent();
            AddUIEvent<PropType, int>(EventDefine.Event_PropCountChanged, OnPropCountChanged);
        }

        public void Refresh()
        {
            var gamePlay = GameManager.Instance?.CurrentGamePlay as CorePlayGamePlay;
            if (_corePlayGamePlay != gamePlay)
            {
                if (_corePlayGamePlay != null)
                    _corePlayGamePlay.OnSelectionChanged -= OnSelectionChanged;
                _corePlayGamePlay = gamePlay;
                if (_corePlayGamePlay != null && _propType == PropType.Reset)
                    _corePlayGamePlay.OnSelectionChanged += OnSelectionChanged;
            }
            RefreshDisplay();
            RefreshResetIcon(false);
        }

        protected override void OnDestroy()
        {
            _isDestroyed = true;
            if (_corePlayGamePlay != null)
                _corePlayGamePlay.OnSelectionChanged -= OnSelectionChanged;
            StopIconSwitch();
            base.OnDestroy();
        }

        private async UniTaskVoid LoadResetIconsAsync()
        {
            if (_resetIconsLoading || (_selectAllIcon != null && _clearIcon != null)) return;
            _resetIconsLoading = true;
            try
            {
                var token = gameObject.GetCancellationTokenOnDestroy();
                var (selectAllIcon, clearIcon) = await UniTask.WhenAll(
                    GameModule.Resource.LoadAssetAsync<Sprite>(SelectAllIconPath, token),
                    GameModule.Resource.LoadAssetAsync<Sprite>(ClearIconPath, token));
                if (_isDestroyed || _iconImage == null) return;
                _selectAllIcon = selectAllIcon;
                _clearIcon = clearIcon;
                RefreshResetIcon(false);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception exception)
            {
                Log.Error($"[CorePlayPropWidget] 全选/清空图标加载失败: {exception}");
            }
            finally
            {
                _resetIconsLoading = false;
            }
        }

        private void OnSelectionChanged()
        {
            RefreshResetIcon(true);
        }

        private void RefreshResetIcon(bool animate)
        {
            if (_propType != PropType.Reset || _isDestroyed || _iconImage == null) return;
            var target = _corePlayGamePlay != null && _corePlayGamePlay.SelectedStrokeIndices.Count > 0
                ? _clearIcon : _selectAllIcon;
            if (target == null || (animate && target == _resetIconTarget)) return;

            // 快速连续操作时中断旧动画，始终以最新选择状态为准。
            StopIconSwitch();
            _resetIconTarget = target;
            if (!animate || !_iconImage.gameObject.activeInHierarchy || _iconImage.sprite == target)
            {
                _iconImage.sprite = target;
                return;
            }

            _iconSwitchTween = DOTween.Sequence();
            _iconSwitchTween.SetUpdate(true);
            _iconSwitchTween.Append(_iconImage.transform.DOScale(Vector3.zero, 0.08f).SetEase(Ease.InQuad));
            _iconSwitchTween.AppendCallback(() => _iconImage.sprite = target);
            _iconSwitchTween.Append(_iconImage.transform.DOScale(_iconOriginalScale, 0.14f).SetEase(Ease.OutBack));
            _iconSwitchTween.OnComplete(() => _iconSwitchTween = null);
        }

        private void StopIconSwitch()
        {
            _iconSwitchTween?.Kill();
            _iconSwitchTween = null;
            if (_iconImage != null) _iconImage.transform.localScale = _iconOriginalScale;
        }

        // ================ 点击分发 ================

        /// <summary>道具按钮点击</summary>
        private void OnPropClicked()
        {
            switch (_propType)
            {
                case PropType.Tip:
                    UseTipProp();
                    break;
                case PropType.Reset:
                    UseResetProp();
                    break;
                case PropType.Next:
                    UseNextPropAsync().Forget();
                    break;
            }
        }

        /// <summary>使用提示道具</summary>
        private void UseTipProp()
        {
            if (_corePlayGamePlay == null) return;

            // 检查是否还有未找到的答案
            if (!_corePlayGamePlay.HasUnfoundAnswers())
            {
                DebugLog("没有剩余答案可提示");
                return;
            }

            // 三档付费：数量优先、其次金币、最后广告
            if (!TryPayProp(() => PropDefine.UseTip(), out bool needRefresh))
            {
                DebugLog("无可用使用方式（数量/金币/广告均不可用）");
                return;
            }

            // 获取随机未找到答案的一组笔画
            List<int> strokeSet = _corePlayGamePlay.GetRandomUnfoundAnswerStrokeSet();
            if (strokeSet == null || strokeSet.Count == 0)
            {
                DebugLogError("获取提示笔画失败");
                return;
            }

            // 发送高亮事件，触发闪烁效果
            GameEvent.Send(EventDefine.Event_PropTipHighlight, strokeSet);
            DebugLog($"使用提示道具，高亮笔画: [{string.Join(", ", strokeSet)}]");

            if (needRefresh) RefreshDisplay();
        }

        /// <summary>无选中笔画时全选，否则清空选择；两种操作均免费，不影响已找到答案。</summary>
        private void UseResetProp()
        {
            if (_corePlayGamePlay == null || !_corePlayGamePlay.IsGameRunning) return;
            if (_corePlayGamePlay.SelectedStrokeIndices.Count == 0)
            {
                GameEvent.Send(EventDefine.Event_PropTipClearHighlight);
                _corePlayGamePlay.SelectAllStrokes();
                DebugLog("使用全选道具，选中全部笔画");
                return;
            }

            _corePlayGamePlay.ClearSelection();
            GameEvent.Send(EventDefine.Event_PropResetDone);
            DebugLog("使用重置道具，清空当前选中笔画");
        }

        /// <summary>使用下一关道具：补齐答案，逐个播放入槽动画，最后触发统一结算</summary>
        private async UniTaskVoid UseNextPropAsync()
        {
            if (_corePlayGamePlay == null || !_corePlayGamePlay.CanUseNextProp()) return;

            var corePlayView = GameManager.Instance?.CurrentView;
            if (corePlayView == null) return;

            using (UIInteractionLock.Acquire())
            {
                if (!TryPayProp(() => PropDefine.UseNext(), out bool needRefresh))
                {
                    DebugLog("无可用使用方式（数量/金币/广告均不可用）");
                    return;
                }

                if (!_corePlayGamePlay.PrepareNextPropCompletion(out List<string> answerCharacters))
                {
                    DebugLogError("下一关道具补齐答案失败");
                    return;
                }

                if (needRefresh) RefreshDisplay();
                GameManager.Instance.SaveGameProgress();
                try
                {
                    await corePlayView.PlayNextPropAnswersAsync(answerCharacters, 10);
                }
                catch (System.OperationCanceledException)
                {
                    return;
                }
                _corePlayGamePlay.CompletePreparedLevel();
            }
        }

        // ================ 三档付费状态机 ================

        /// <summary>
        /// 按优先级尝试付费：数量 → 金币 → 广告。
        /// </summary>
        /// <param name="useByCount">数量付费执行体（已扣数量并返回 true）</param>
        /// <param name="paidByNonCount">是否经金币或广告付费（需返回后刷新显示）</param>
        /// <returns>是否付费成功</returns>
        private bool TryPayProp(System.Func<bool> useByCount, out bool paidByNonCount)
        {
            paidByNonCount = false;

            // 1) 数量付费
            if (PropDefine.IsPropAvailable(_propType))
            {
                return useByCount();
            }

            // 2) 金币付费（当场扣金币直接使用）
            if (PropDefine.CoinCount >= CoinCost)
            {
                if (PropDefine.UsePropByCoin(CoinCost))
                {
                    GameEvent.Send(EventDefine.Event_UITopCoinAddAnim, -CoinCost);
                    paidByNonCount = true;
                    return true;
                }
            }

            // 3) 广告付费（当前占位 IsAdAvailable 恒 false，此分支不会进入）
            // 接入激励广告后：先 ShowRewardedAd，成功回调中执行与金币分支等价的道具效果逻辑。
            // 因广告为异步回调流程，此处返回 false，真实接入时需重构为回调驱动使用。
            return false;
        }

        // ================ 显示刷新 ================

        /// <summary>道具数量变化回调</summary>
        private void OnPropCountChanged(PropType propType, int newCount)
        {
            // 金币变化或自身数量变化都需刷新（金币影响金币节点显示）
            if (propType == _propType || propType == PropType.Coin)
            {
                RefreshDisplay();
            }
        }

        /// <summary>刷新数量/金币/广告三档显示与按钮置灰</summary>
        private void RefreshDisplay()
        {
            // Reset 为免费功能，不展示或消耗数量、金币、广告。
            if (_propType == PropType.Reset)
            {
                SetActive(_countRoot, false);
                SetActive(_coinRoot, false);
                SetActive(_adRoot, false);
                ApplyAvailability(true);
                return;
            }

            bool hasCount = PropDefine.IsPropAvailable(_propType);
            bool hasCoin = PropDefine.CoinCount >= CoinCost;
            bool hasAd = AdSystem.IsAdAvailable;

            bool canUse;
            if (hasCount)
            {
                // 数量优先：显示数量，隐藏金币/广告
                SetActive(_countRoot, true);
                if (_countText != null) _countText.text = PropDefine.GetPropCount(_propType).ToString();
                SetActive(_coinRoot, false);
                SetActive(_adRoot, false);
                canUse = true;
            }
            else if (hasCoin)
            {
                // 金币替代：隐藏数量，显示金币节点
                SetActive(_countRoot, false);
                SetActive(_coinRoot, true);
                if (_coinText != null) _coinText.text = CoinCost.ToString();
                SetActive(_adRoot, false);
                canUse = true;
            }
            else if (hasAd)
            {
                // 广告替代：隐藏数量/金币，显示广告
                SetActive(_countRoot, false);
                SetActive(_coinRoot, false);
                SetActive(_adRoot, true);
                canUse = true;
            }
            else
            {
                // 全不可用：全部隐藏，置灰
                SetActive(_countRoot, false);
                SetActive(_coinRoot, false);
                SetActive(_adRoot, false);
                canUse = false;
            }

            ApplyAvailability(canUse);
        }

        private void ApplyAvailability(bool canUse)
        {
            Color displayColor = canUse ? Color.white : Color.gray;
            if (_backgroundImage != null) _backgroundImage.color = displayColor;
            if (_iconImage != null) _iconImage.color = displayColor;
            if (_propButton != null) _propButton.Interactable = canUse;
        }

        /// <summary>安全切换节点显隐（节点可能未在 Prefab 配置）</summary>
        private void SetActive(Component comp, bool active)
        {
            if (comp != null && comp.gameObject != null)
            {
                comp.gameObject.SetActive(active);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void DebugLog(string msg)
        {
            Log.Info($"[CorePlayPropWidget] {msg}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void DebugLogError(string msg)
        {
            Log.Error($"[CorePlayPropWidget] {msg}");
        }
    }
}
