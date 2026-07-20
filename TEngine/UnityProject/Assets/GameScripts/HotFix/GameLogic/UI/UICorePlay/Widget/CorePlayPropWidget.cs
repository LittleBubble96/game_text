using System.Collections.Generic;
using GameLogic.GamePlay.CorePlay;
using TEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// CorePlay 道具按钮 Widget —— 支持 Tip（提示）等道具类型。
    /// 提示道具：点击后高亮闪烁一个未找到的答案笔画组合，持续2秒或直到用户选中笔画。
    /// </summary>
    public class CorePlayPropWidget : UIWidget
    {
        [SerializeField] private PropType _propType = PropType.Tip;

        private XYButton _propButton;
        private TMP_Text _countText;
        private Image _iconImage;
        private GameObject _disabledMask;

        private CorePlayGamePlay _corePlayGamePlay;

        protected override void OnCreate()
        {
            base.OnCreate();
            _propButton = CreateWidget<XYButton>("");
            _countText = FindChildComponent<TMP_Text>("Count");
            _iconImage = FindChildComponent<Image>("Icon");
            _disabledMask = FindChild("DisabledMask")?.gameObject;

            _propButton.OnAddListener(OnPropClicked);
        }

        protected override void RegisterEvent()
        {
            base.RegisterEvent();
            AddUIEvent<PropType, int>(EventDefine.Event_PropCountChanged, OnPropCountChanged);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            _corePlayGamePlay = GameManager.Instance?.CurrentGamePlay as CorePlayGamePlay;
            RefreshCountDisplay();
            RefreshInteractable();
        }

        /// <summary>道具按钮点击</summary>
        private void OnPropClicked()
        {
            if (_propType == PropType.Tip)
            {
                UseTipProp();
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

            // 检查道具数量
            if (!PropDefine.UseTip())
            {
                DebugLog("提示道具数量不足");
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
        }

        /// <summary>道具数量变化回调</summary>
        private void OnPropCountChanged(PropType propType, int newCount)
        {
            if (propType == _propType)
            {
                RefreshCountDisplay();
                RefreshInteractable();
            }
        }

        /// <summary>刷新数量显示</summary>
        private void RefreshCountDisplay()
        {
            if (_countText != null)
            {
                int count = _propType == PropType.Tip ? PropDefine.TipCount : PropDefine.CoinCount;
                _countText.text = count.ToString();
            }
        }

        /// <summary>刷新可交互状态</summary>
        private void RefreshInteractable()
        {
            bool canUse = false;

            if (_propType == PropType.Tip)
            {
                canUse = PropDefine.IsTipAvailable && _corePlayGamePlay != null && _corePlayGamePlay.HasUnfoundAnswers();
            }

            if (_propButton != null)
            {
                _propButton.gameObject.SetActive(canUse);
            }

            if (_disabledMask != null)
            {
                _disabledMask.SetActive(!canUse);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void DebugLog(string msg)
        {
            UnityEngine.Debug.Log($"[CorePlayPropWidget] {msg}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void DebugLogError(string msg)
        {
            UnityEngine.Debug.LogError($"[CorePlayPropWidget] {msg}");
        }
    }
}