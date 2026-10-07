using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameConfig;
using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.Top, location: "UIFinish")]
    public class UIFinish : UIWindow
    {
        private RTLTextMeshPro _nextBtnText;
        private RTLTextMeshPro _homeBtnText;
        private RTLTextMeshPro _adBtnText;
        private RTLTextMeshPro _titleText;
        private RTLTextMeshPro _desText;
        private RTLTextMeshPro _rewardText;

        private XYButton _btnNext;
        private XYButton _btnHome;
        private XYButton _btnAd;
        private Graphic[] _adGraphics;
        private Color[] _adOriginalColors;
        private Transform _adRoot;
        private Transform _adLoading;
        private Transform _adSuccess;
        private Transform _adSpinner;
        private Animation _adSpinnerAnimation;
        private GameObject _btnNextGo;

        #region 奖励
        private RectTransform _rewardRoot;
        private RewardItemWidget _rewardItemCoinWidget;
        private RewardItemWidget _rewardItemPropWidget;
        private readonly List<RewardItemWidget> _propRewardWidgets = new List<RewardItemWidget>();
        private readonly List<RewardItemWidget> _activeRewardWidgets = new List<RewardItemWidget>();

        #endregion

        private Animation _animation;
        private string _animShowName = "UIFinishShowAnim";
        private string _animHideName = "UIFinishHideAnim";

        private int _completedLevelId;

        /// <summary>奖励数据：itemId -> 数量</summary>
        private Dictionary<int, int> _rewardMap;

        /// <summary>是否已领取奖励</summary>
        private bool _hasClaimedReward;

        /// <summary>是否有下一关</summary>
        private bool _hasNextLevel;
        private bool _doubleRewardInFlight;
        private bool _doubleRewardClaimed;
        private int _rewardVersion;

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _animation = transform.GetComponent<Animation>();
            _btnNext = CreateWidget<XYButton>("VictoryPanel/ButtonNext");
            _btnHome = CreateWidget<XYButton>("VictoryPanel/ButtonHome");
            _btnAd = CreateWidget<XYButton>("VictoryPanel/ButtonAd");
            _adGraphics = _btnAd.gameObject.GetComponentsInChildren<Graphic>(true);
            _adOriginalColors = new Color[_adGraphics.Length];
            for (int i = 0; i < _adGraphics.Length; i++) _adOriginalColors[i] = _adGraphics[i].color;
            _adRoot = FindChildComponent<Transform>("VictoryPanel/ButtonAd/adRoot");
            _adLoading = FindChildComponent<Transform>("VictoryPanel/ButtonAd/adRoot/m_adLoadingRoot");
            _adSuccess = FindChildComponent<Transform>("VictoryPanel/ButtonAd/adRoot/m_adSuccessRoot");
            _adSpinner = FindChildComponent<Transform>("VictoryPanel/ButtonAd/adRoot/m_adLoadingRoot/loading");
            if (_adSpinner != null) _adSpinnerAnimation = _adSpinner.GetComponent<Animation>();

            _nextBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonNext/Text");
            _homeBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonHome/Text");
            _adBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonAd/Text");
            _titleText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/Title");
            _desText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/m_des");
            _rewardText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/RewardRoot/Reward");
            _btnNextGo = _btnNext.gameObject;
            _btnNext.OnAddListener(OnBtnNextClick);
            _btnHome.OnAddListener(OnBtnHomeClick);
            _btnAd.OnAddListener(OnBtnDoubleRewardClick);

            _rewardRoot = FindChildComponent<RectTransform>("VictoryPanel/RewardRoot");
            _rewardItemCoinWidget = CreateWidget<RewardItemWidget>("VictoryPanel/RewardRoot/RewardBg/RewardCoinItem");
            _rewardItemPropWidget = CreateWidget<RewardItemWidget>("VictoryPanel/RewardRoot/RewardBg/RewardTipsItem");
            if (_rewardItemPropWidget != null)
                _propRewardWidgets.Add(_rewardItemPropWidget);
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            GameEvent.Send(EventDefine.Event_UITopUpdate, new UITopData(showCoin: true, showBack: false));
            // 不在此处同步金币数：通关时已加金币到数据层，但顶部金币栏保持旧值，
            // 待玩家点击结算按钮时由 Event_UITopCoinAddAnim 从旧值滚动到新值。

            if (_userDatas != null && _userDatas.Length > 0 && _userDatas[0] is int levelId)
            {
                _completedLevelId = levelId;
            }

            _hasNextLevel = GameManager.Instance.LevelConfig?.GetLevelNameByLevelId(_completedLevelId + 1) != null;
            if (_btnNextGo != null)
            {
                _btnNextGo.SetActive(_hasNextLevel);
            }

            BiMgr.FinishShown();

            // 加载奖励数据（内部异步加载奖励图标，fire-and-forget）
            LoadRewardData();
            _rewardVersion = GameManager.Instance.CompletionRewardVersion;
            _doubleRewardInFlight = false;
            _doubleRewardClaimed = GameManager.Instance.IsDoubleRewardClaimed(_completedLevelId, _rewardVersion);
            if (_doubleRewardClaimed)
            {
                foreach (var key in new List<int>(_rewardMap.Keys)) _rewardMap[key] *= 2;
                RefreshRewardWidgets();
            }
            AdSystem.StateChanged -= OnAdStateChanged;
            AdSystem.StateChanged += OnAdStateChanged;
            AdSystem.EnterPage(AdIds.DoubleReward);
            RefreshDoubleRewardButton();
            RefreshText();
        }

        protected override void OnClose()
        {
            AdSystem.StateChanged -= OnAdStateChanged;
            AdSystem.ExitPage(AdIds.DoubleReward);
            base.OnClose();
        }

        protected override void OnDestroy()
        {
            AdSystem.StateChanged -= OnAdStateChanged;
            AdSystem.ExitPage(AdIds.DoubleReward);
            base.OnDestroy();
        }

        private void OnAdStateChanged(string adId)
        {
            if (IsOpen && adId == AdIds.DoubleReward) RefreshDoubleRewardButton();
        }

        private void RefreshDoubleRewardButton()
        {
            if (_btnAd == null) return;
            bool isLoading = AdSystem.IsLoading(AdIds.DoubleReward);
            bool isReady = AdSystem.IsAdAvailable(AdIds.DoubleReward);
            // if (_adRoot != null) _adRoot.gameObject.SetActive(!_doubleRewardClaimed && (isLoading || isReady));
            if (_adLoading != null) _adLoading.gameObject.SetActive(isLoading);
            if (_adSuccess != null) _adSuccess.gameObject.SetActive(isReady);
            _btnAd.Interactable = !_hasClaimedReward && !_doubleRewardClaimed && !_doubleRewardInFlight &&
                !isLoading && isReady && GameManager.Instance.CanClaimDoubleReward(_completedLevelId, _rewardVersion);
            for (int i = 0; i < _adGraphics.Length; i++)
                if (_adGraphics[i] != null)
                    _adGraphics[i].color = _doubleRewardClaimed ? _adOriginalColors[i] * Color.gray : _adOriginalColors[i];
            RefreshDoubleRewardText();
        }

        protected override void OnUpdate()
        {
            if (_adSpinner != null && _adSpinner.gameObject.activeInHierarchy &&
                AdSystem.IsLoading(AdIds.DoubleReward) &&
                (_adSpinnerAnimation == null || !_adSpinnerAnimation.enabled))
                _adSpinner.Rotate(0, 0, -240f * Time.unscaledDeltaTime);
        }

        protected override void OnInAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animShowName, () =>
            {
                CompleteInAnimation();
                // 入场动画完成后播放入场奖励动画
                // PlayRewardShowAnim();
            }, CloseToken).Forget();
        }

        protected override void OnOutAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animHideName, CompleteOutAnimation, CloseToken).Forget();
        }

        #region 奖励逻辑

        /// <summary>从配置加载当前关卡奖励</summary>
        private void LoadRewardData()
        {
            _rewardMap = new Dictionary<int, int>();
            _hasClaimedReward = false;
            SetButtonsInteractable(true);
            _activeRewardWidgets.Clear();
            if (_rewardItemCoinWidget != null)
                _rewardItemCoinWidget.Visible = false;
            foreach (var widget in _propRewardWidgets)
                widget.Visible = false;
            // if (_rewardRoot != null)
            //     _rewardRoot.gameObject.SetActive(false);

            // 获取关卡配置中的 RewardId
            var tbLevel = ConfigSystem.Instance.Tables.TbLevel;
            if (tbLevel == null || !tbLevel.DataMap.TryGetValue(_completedLevelId, out var confLevel))
                return;

            int rewardId = confLevel.RewardId;
            if (rewardId <= 0) return;

            // 查奖励表
            var tbReward = ConfigSystem.Instance.Tables.TbReward;
            var confReward = tbReward?.GetOrDefault(rewardId);
            if (confReward?.Rewards == null || confReward.Rewards.Count == 0) return;

            _rewardMap = new Dictionary<int, int>(confReward.Rewards);

            RefreshRewardWidgets();
        }

        /// <summary>金币使用专用节点，其余道具复用通用模板。</summary>
        private void RefreshRewardWidgets()
        {
            var tbItem = ConfigSystem.Instance.Tables.TbItem;
            int propIndex = 0;
            foreach (var reward in _rewardMap)
            {
                if (reward.Value <= 0) continue;
                var itemCfg = tbItem?.GetOrDefault(reward.Key);
                if (itemCfg == null) continue;
                RewardItemWidget widget;
                if (reward.Key == ItemId.Coin)
                    widget = _rewardItemCoinWidget;
                else
                {
                    if (_rewardItemPropWidget == null) continue;
                    if (propIndex >= _propRewardWidgets.Count)
                    {
                        var extra = CreateWidgetByPrefab<RewardItemWidget>(
                            _rewardItemPropWidget.gameObject, _rewardItemPropWidget.transform.parent, false);
                        if (extra == null) continue;
                        _propRewardWidgets.Add(extra);
                    }
                    widget = _propRewardWidgets[propIndex++];
                }
                if (widget == null) continue;
                widget.Visible = true;
                widget.SetReward(null, reward.Value);
                _activeRewardWidgets.Add(widget);
                LoadRewardIconAsync(widget, itemCfg.ResIcon, _rewardMap).Forget();
            }
            if (_rewardRoot != null)
                _rewardRoot.gameObject.SetActive(_activeRewardWidgets.Count > 0);

            // 所有子节点创建、显隐和缩放恢复后，统一重新计算奖励布局。
            if (_activeRewardWidgets.Count > 0 &&
                _activeRewardWidgets[0].transform.parent is RectTransform layoutRoot)
            {
                LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
                if (layoutRoot.gameObject.activeInHierarchy)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
            }
        }

        private async UniTaskVoid LoadRewardIconAsync(RewardItemWidget widget, string iconPath,
            Dictionary<int, int> rewardMap)
        {
            if (string.IsNullOrEmpty(iconPath)) return;
            var sprite = await GameModule.Resource.LoadAssetAsync<Sprite>(iconPath);
            // 防止旧图标加载结果覆盖下一次结算或重置领取动画。
            if (IsOpen && _rewardMap == rewardMap && !_hasClaimedReward && widget.gameObject != null)
                widget.SetIcon(sprite);
        }

        /// <summary>播放奖励展示动画</summary>
        private void PlayRewardShowAnim()
        {
            float delay = 0f;
            foreach (var widget in _activeRewardWidgets)
            {
                widget.PlayShowAnim(delay);
                delay += 0.2f;
            }
        }

        /// <summary>领取奖励：仅播放奖励飞行动画表现（数据已在通关时由 GameManager 发放）</summary>
        private void ClaimReward(System.Action onComplete)
        {
            if (_hasClaimedReward || _rewardMap == null || _rewardMap.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _hasClaimedReward = true;
            SetButtonsInteractable(false);

            // 奖励数据已在通关时发放，此处只播特效表现。
            if (_rewardMap.TryGetValue(ItemId.Coin, out int coinCount) && coinCount > 0)
                GameEvent.Send(EventDefine.Event_UITopCoinAddAnim, coinCount);
            DoClaimAnimations(onComplete).Forget();
        }

        private async UniTaskVoid DoClaimAnimations(System.Action onComplete)
        {
            int version = OpenVersion;
            var token = CloseToken;
            // 只等待实际创建的动画；没有可展示奖励时也必须完成结算。
            var animations = new List<UniTask>();
            foreach (var widget in _activeRewardWidgets)
            {
                var completion = new UniTaskCompletionSource();
                widget.PlayFlyAnim(0.6f, () => completion.TrySetResult());
                animations.Add(completion.Task);
            }
            if (await UniTask.WhenAll(animations).AttachExternalCancellation(token).SuppressCancellationThrow()) return;
            if (!IsOpen || version != OpenVersion) return;
            if (_rewardRoot != null)
                _rewardRoot.gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        #endregion

        #region 按钮交互

        private void SetButtonsInteractable(bool interactable)
        {
            _btnNext.Interactable = interactable;
            _btnHome.Interactable = interactable;
            if (!interactable) _btnAd.Interactable = false;
            else RefreshDoubleRewardButton();
        }

        private void OnBtnDoubleRewardClick()
        {
            if (_hasClaimedReward || _doubleRewardInFlight || _doubleRewardClaimed ||
                !AdSystem.IsAdAvailable(AdIds.DoubleReward)) return;
            ClaimDoubleRewardAsync().Forget();
        }

        private async UniTask ClaimDoubleRewardAsync()
        {
            int openVersion = OpenVersion;
            int levelId = _completedLevelId;
            int rewardVersion = _rewardVersion;
            _doubleRewardInFlight = true;
            SetButtonsInteractable(false);
            try
            {
                bool completed = await AdSystem.ShowRewardedAdAsync(AdIds.DoubleReward);
                if (!completed || !IsOpen || openVersion != OpenVersion || levelId != _completedLevelId) return;
                if (!GameManager.Instance.TryClaimDoubleReward(levelId, rewardVersion, completed)) return;
                _doubleRewardClaimed = true;
                // 数据已经补发；结算展示与离场动画改为两份奖励，总金币为 20 + 20。
                foreach (var key in new List<int>(_rewardMap.Keys)) _rewardMap[key] *= 2;
                RefreshRewardWidgets();
                // 顶部继续保持原值，离场时统一播放两份奖励的加币动画，避免显示重复累加。
            }
            finally
            {
                if (IsOpen && openVersion == OpenVersion)
                {
                    _doubleRewardInFlight = false;
                    SetButtonsInteractable(!_hasClaimedReward);
                }
            }
        }

        private void OnBtnNextClick()
        {
            if (_doubleRewardInFlight) return;
            BiMgr.FinishAction("next");
            if (_hasClaimedReward)
            {
                // 已领取，直接跳转
                GameModule.UI.CloseUI<UIFinish>();
                GameManager.Instance.LoadNextCorePlayLevel();
                return;
            }

            // 领取奖励 → 播放特效 → 跳转下一关
            ClaimReward(() =>
            {
                GameModule.UI.CloseUI<UIFinish>();
                GameManager.Instance.LoadNextCorePlayLevel();
            });
        }

        private void OnBtnHomeClick()
        {
            if (_doubleRewardInFlight) return;
            BiMgr.FinishAction("home");
            if (_hasClaimedReward)
            {
                GameManager.Instance.ReturnToHome();
                return;
            }

            // 领取奖励 → 播放特效 → 回主页
            ClaimReward(() =>
            {
                GameManager.Instance.ReturnToHome();
            });
        }

        #endregion

        private void RefreshText()
        {
            _nextBtnText.text = LocalizationHelper.GetLocalText(LanguageKey.next_level_btn);
            _homeBtnText.text = LocalizationHelper.GetLocalText(LanguageKey.back_btn);
            _titleText.text = LocalizationHelper.GetLocalText(LanguageKey.finish_title);
            _desText.text = LocalizationHelper.GetLocalText(LanguageKey.finish_des);
            RefreshDoubleRewardText();
            _rewardText.text = LocalizationHelper.GetLocalText(LanguageKey.finish_reward);
        }

        private void RefreshDoubleRewardText()
        {
            _adBtnText.text = LocalizationHelper.GetLocalText(_doubleRewardClaimed
                ? LanguageKey.finish_double_reward_claimed : LanguageKey.finish_double_reward);
        }
    }
}
