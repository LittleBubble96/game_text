using System.Collections.Generic;
using GameLogic.GamePlay.CorePlay;
using TEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// CorePlay 道具按钮 Widget —— 支持 Tip（提示）/ Reset（重置）等道具类型。
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
        private Image _iconImage;             // 道具图标（置灰用）

        // 金币替代使用节点（数量为 0 且金币足够时显示）
        private Transform _coinRoot;
        private TMP_Text _coinText;

        // 广告节点（数量与金币都不可用时显示，未来扩展）
        private Transform _adRoot;

        private CorePlayGamePlay _corePlayGamePlay;

        /// <summary>该道具的金币消耗量</summary>
        private int CoinCost => _propType switch
        {
            PropType.Tip => GameDefine.PropTipCoinCost,
            PropType.Reset => GameDefine.PropResetCoinCost,
            _ => int.MaxValue,
        };

        protected override void OnCreate()
        {
            base.OnCreate();
            _propButton = CreateWidget<XYButton>("");
            _countRoot = FindChildComponent<Transform>("CountBg");
            _countText = FindChildComponent<TMP_Text>("CountBg/Count");
            _iconImage = FindChildComponent<Image>("bg");

            _coinRoot = FindChildComponent<Transform>("CoinRoot");
            _coinText = FindChildComponent<TMP_Text>("CoinRoot/CoinText");

            _adRoot = FindChildComponent<Transform>("AdRoot");

            _propButton.OnAddListener(OnPropClicked);
        }

        public void OnInit(PropType propType)
        {
            _propType = propType;
        }

        protected override void RegisterEvent()
        {
            base.RegisterEvent();
            AddUIEvent<PropType, int>(EventDefine.Event_PropCountChanged, OnPropCountChanged);
        }

        public void Refresh()
        {
            _corePlayGamePlay = GameManager.Instance?.CurrentGamePlay as CorePlayGamePlay;
            RefreshDisplay();
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

        /// <summary>使用重置道具：清空当前关已找到的答案并刷新视图</summary>
        private void UseResetProp()
        {
            if (_corePlayGamePlay == null) return;

            // 三档付费
            if (!TryPayProp(() => PropDefine.UseReset(), out bool needRefresh))
            {
                DebugLog("无可用使用方式（数量/金币/广告均不可用）");
                return;
            }

            // 清空答案（数据层）
            if (!_corePlayGamePlay.ClearAnswers())
            {
                DebugLogError("重置失败，当前无有效关卡");
                return;
            }

            // 通知视图层清空高亮与 slot、UI 层刷新进度文字
            GameEvent.Send(EventDefine.Event_PropResetDone);
            DebugLog("使用重置道具，清空已找到答案");

            if (needRefresh) RefreshDisplay();
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

            if (_iconImage != null)
            {
                _iconImage.color = canUse ? Color.white : Color.gray;
            }
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
