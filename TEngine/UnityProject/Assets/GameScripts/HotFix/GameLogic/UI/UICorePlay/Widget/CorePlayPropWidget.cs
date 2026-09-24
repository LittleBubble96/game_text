using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameLogic.Data;
using GameLogic.GamePlay.CorePlay;
using GameLogic.Localization;
using RTLTMPro;
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
    /// 广告接入见 <see cref="AdSystem"/>。
    /// </summary>
    public class CorePlayPropWidget : UIWidget
    {
        private PropType _propType = PropType.Tip;

        private XYButton _propButton;
        private Transform _countRoot;          // 数量节点
        private TMP_Text _countText;          // 数量节点文字
        private RTLTextMeshPro _des;
        private Image _backgroundImage;       // 道具背景（置灰用）
        private Image _iconImage;             // 道具图标（置灰用）

        // 金币替代使用节点（数量为 0 且金币足够时显示）
        private Transform _coinRoot;
        private TMP_Text _coinText;

        // 广告节点（数量与金币都不可用时显示）
        private Transform _adRoot;
        private Transform _adLoading;
        private Transform _adComplete;

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
        private bool _usingProp;
        private int _levelVersion;
        private Transform _adSpinner;
        private string AdId => _propType == PropType.Next ? AdIds.Next : AdIds.Tip;

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
            _des = FindChildComponent<RTLTextMeshPro>("des");
            _backgroundImage = FindChildComponent<Image>("bg");
            _iconImage = FindChildComponent<Image>("icon");
            if (_iconImage != null) _iconOriginalScale = _iconImage.transform.localScale;

            _coinRoot = FindChildComponent<Transform>("CoinRoot");
            _coinText = FindChildComponent<TMP_Text>("CoinRoot/CoinText");

            _adRoot = FindChildComponent<Transform>("adRoot");
            _adLoading = FindChildComponent<Transform>("adRoot/m_adLoadingRoot");
            _adComplete = FindChildComponent<Transform>("adRoot/m_adSuccessRoot");

            _adSpinner = FindChildComponent<Transform>("adRoot/m_adLoadingRoot/loading");
            AdSystem.StateChanged += OnAdStateChanged;
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
                {
                    _corePlayGamePlay.OnSelectionChanged -= OnSelectionChanged;
                    _corePlayGamePlay.OnLevelLoaded -= OnAdLevelLoaded;
                }
                _corePlayGamePlay = gamePlay;
                _levelVersion++;
                if (_corePlayGamePlay != null) _corePlayGamePlay.OnLevelLoaded += OnAdLevelLoaded;
                if (_corePlayGamePlay != null && _propType == PropType.Reset)
                    _corePlayGamePlay.OnSelectionChanged += OnSelectionChanged;
            }
            RefreshDisplay();
            RefreshResetIcon(false);
            RefreshDes();
        }

        protected override void OnDestroy()
        {
            _isDestroyed = true;
            AdSystem.StateChanged -= OnAdStateChanged;
            if (_corePlayGamePlay != null)
            {
                _corePlayGamePlay.OnSelectionChanged -= OnSelectionChanged;
                _corePlayGamePlay.OnLevelLoaded -= OnAdLevelLoaded;
            }
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
            RefreshDes();
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

        private void OnAdStateChanged(string adId)
        {
            // 仅查询自身广告位状态；跨广告位不设置展示互斥。
            RefreshDisplay();
        }

        private void OnAdLevelLoaded(TextLevelData data) { _levelVersion++; }

        private void OnPropClicked() { UsePropAsync().Forget(); }

        private async UniTaskVoid UsePropAsync()
        {
            if (_usingProp || AdSystem.IsBusy(AdId) || _isDestroyed) return;
            _usingProp = true;
            try
            {
                switch (_propType)
                {
                    case PropType.Tip: await UseTipPropAsync(); break;
                    case PropType.Reset: UseResetProp(); break;
                    case PropType.Next: await UseNextPropAsync(); break;
                }
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception error) { Log.Error("[CorePlayPropWidget] 道具使用失败: " + error); }
            finally { _usingProp = false; if (!_isDestroyed) RefreshDisplay(); }
        }

        /// <summary>使用提示道具</summary>
        private async UniTask UseTipPropAsync()
        {
            if (_corePlayGamePlay == null || !_corePlayGamePlay.IsGameRunning) return;

            // 检查是否还有未找到的答案
            if (!_corePlayGamePlay.HasUnfoundAnswers())
            {
                BiMgr.PropUsed("tip", "none", 0, false, "no_remaining_answer");
                DebugLog("没有剩余答案可提示");
                return;
            }

            // 三档付费：数量优先、其次金币、最后广告
            var (paid, payment, cost) = await PayPropAsync(() => PropDefine.UseTip());
            if (_isDestroyed || payment == "stale") return;
            if (!paid)
            {
                BiMgr.PropUsed("tip", payment, cost, false, "payment_failed");
                DebugLog("无可用使用方式（数量/金币/广告均不可用）");
                return;
            }

            // 获取随机未找到答案的一组笔画
            List<int> strokeSet = _corePlayGamePlay.GetRandomUnfoundAnswerStrokeSet();
            if (strokeSet == null || strokeSet.Count == 0)
            {
                BiMgr.PropUsed("tip", payment, cost, false, "invalid_answer_strokes");
                DebugLogError("获取提示笔画失败");
                return;
            }

            // 发送高亮事件，触发闪烁效果
            GameEvent.Send(EventDefine.Event_PropTipHighlight, strokeSet);
            BiMgr.PropUsed("tip", payment, cost, true, "");
            DebugLog($"使用提示道具，高亮笔画: [{string.Join(", ", strokeSet)}]");

            RefreshDisplay();
        }

        /// <summary>无选中笔画时全选，否则清空选择；两种操作均免费，不影响已找到答案。</summary>
        private void UseResetProp()
        {
            if (_corePlayGamePlay == null || !_corePlayGamePlay.IsGameRunning) return;
            if (_corePlayGamePlay.SelectedStrokeIndices.Count == 0)
            {
                _corePlayGamePlay.SelectAllStrokes();
                DebugLog("使用全选道具，选中全部笔画");
                return;
            }

            _corePlayGamePlay.ClearSelection();
            GameEvent.Send(EventDefine.Event_PropResetDone);
            DebugLog("使用重置道具，清空当前选中笔画");
        }

        /// <summary>使用下一关道具：补齐答案，逐个播放入槽动画，最后触发统一结算</summary>
        private async UniTask UseNextPropAsync()
        {
            if (_corePlayGamePlay == null || !_corePlayGamePlay.CanUseNextProp()) return;

            var corePlayView = GameManager.Instance?.CurrentView;
            if (corePlayView == null) return;

            var (paid, payment, cost) = await PayPropAsync(() => PropDefine.UseNext());
            if (_isDestroyed || payment == "stale") return;
            if (!paid)
            {
                BiMgr.PropUsed("next", payment, cost, false, "payment_failed");
                return;
            }
            using (UIInteractionLock.Acquire())
            {
                if (!_corePlayGamePlay.PrepareNextPropCompletion(out List<string> answerCharacters))
                {
                    BiMgr.PropUsed("next", payment, cost, false, "completion_prepare_failed");
                    DebugLogError("下一关道具补齐答案失败");
                    return;
                }

                BiMgr.PropUsed("next", payment, cost, true, "");
                RefreshDisplay();
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

        /// <summary>库存、金币即时支付；广告必须等待完整观看。</summary>
        private async UniTask<(bool paid, string payment, int cost)> PayPropAsync(System.Func<bool> useByCount)
        {
            if (PropDefine.IsPropAvailable(_propType))
            {
                bool paid = useByCount();
                return (paid, "inventory", paid ? 1 : 0);
            }
            if (PropDefine.CoinCount >= CoinCost)
            {
                bool paid = PropDefine.UsePropByCoin(CoinCost, _propType == PropType.Tip ? "tip" : "next");
                if (paid) GameEvent.Send(EventDefine.Event_UITopCoinAddAnim, -CoinCost);
                return (paid, "coin", paid ? CoinCost : 0);
            }
            int version = _levelVersion;
            bool rewarded = await AdSystem.ShowRewardedAdAsync(AdId);
            // 超时后可能已退出或切关；迟到奖励不能作用于另一局。
            if (_isDestroyed || version != _levelVersion || _corePlayGamePlay == null || !_corePlayGamePlay.IsGameRunning)
                return (false, "stale", 0);
            if (!rewarded)
                GameEvent.Send(EventDefine.Event_AnswerSubmitted, false, "", LocalizationHelper.GetLocalText(LanguageKey.game_tips5));
            return (rewarded, "ad", 0);
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

        private void RefreshDes()
        {
            if (_propType == PropType.Reset)
            {
                var target = _corePlayGamePlay != null && _corePlayGamePlay.SelectedStrokeIndices.Count > 0;
                _des.text = LocalizationHelper.GetLocalText(target ? LanguageKey.game_prop_clear : LanguageKey.game_prop_select_all);
            }
            else if (_propType == PropType.Next)
            {
                _des.text = LocalizationHelper.GetLocalText(LanguageKey.game_prop_next);
            }
            else if (_propType == PropType.Tip)
            {
                _des.text = LocalizationHelper.GetLocalText(LanguageKey.game_prop_tip);
            }
        }

        /// <summary>刷新数量/金币/广告三档显示与按钮置灰</summary>
        private void RefreshDisplay()
        {
            if (_isDestroyed) return;
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
            bool hasAd = AdSystem.IsAdAvailable(AdId);
            SetActive(_adLoading, AdSystem.IsLoading(AdId));
            SetActive(_adComplete, hasAd);

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
            else if (hasAd || AdSystem.IsLoading(AdId))
            {
                // 广告替代：隐藏数量/金币，显示广告
                SetActive(_countRoot, false);
                SetActive(_coinRoot, false);
                SetActive(_adRoot, true);
                canUse = hasAd;
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

        protected override void OnUpdate()
        {
            if (_adSpinner != null && _adSpinner.gameObject.activeInHierarchy && AdSystem.IsLoading(AdId))
            {
                var anim = _adSpinner.GetComponent<Animation>();
                if (anim == null || !anim.enabled)
                    _adSpinner.Rotate(0, 0, -240f * Time.unscaledDeltaTime);
            }
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
