using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameConfig;
using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    [Window(UILayer.Top, location: "UIFinish")]
    public class UIFinish : UIWindow
    {
        private RTLTextMeshPro _nextBtnText;
        private RTLTextMeshPro _homeBtnText;
        private XYButton _btnNext;
        private XYButton _btnHome;
        private GameObject _btnNextGo;

        #region 奖励
        private RectTransform _rewardRoot;
        private RewardItemWidget _rewardItemCoinWidget;
        private RewardItemWidget _rewardItemTipPropWidget;

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

        protected override void ScriptGenerator()
        {
            base.ScriptGenerator();
            _animation = transform.GetComponent<Animation>();
            _btnNext = CreateWidget<XYButton>("VictoryPanel/ButtonNext");
            _btnHome = CreateWidget<XYButton>("VictoryPanel/ButtonBack");
            _nextBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonNext/Text");
            _homeBtnText = this.FindChildComponent<RTLTextMeshPro>("VictoryPanel/ButtonBack/Text");
            _btnNextGo = _btnNext.gameObject;
            _btnNext.OnAddListener(OnBtnNextClick);
            _btnHome.OnAddListener(OnBtnHomeClick);

            _rewardRoot = FindChildComponent<RectTransform>("VictoryPanel/RewardRoot");
            _rewardItemCoinWidget = CreateWidget<RewardItemWidget>("VictoryPanel/RewardRoot/RewardBg/RewardCoinItem");
            _rewardItemTipPropWidget = CreateWidget<RewardItemWidget>("VictoryPanel/RewardRoot/RewardBg/RewardTipsItem");
        }

        protected override void OnRefresh()
        {
            base.OnRefresh();
            GameEvent.Send(EventDefine.Event_UITopUpdate, new UITopData(showCoin: true, showBack: false));
            GameEvent.Send(EventDefine.Event_UITopCoinUpdate, PropDefine.CoinCount);

            if (_userDatas != null && _userDatas.Length > 0 && _userDatas[0] is int levelId)
            {
                _completedLevelId = levelId;
            }

            _hasNextLevel = GameManager.Instance.LevelConfig?.GetLevelNameByLevelId(_completedLevelId + 1) != null;
            if (_btnNextGo != null)
            {
                _btnNextGo.SetActive(_hasNextLevel);
            }

            // 加载奖励数据
            LoadRewardData();

            RefreshText();
        }

        protected override void OnInAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animShowName, () =>
            {
                CompleteInAnimation();
                // 入场动画完成后播放入场奖励动画
                PlayRewardShowAnim();
            }).Forget();
        }

        protected override void OnOutAnimation()
        {
            _animation.PlayAnimWithDelayAnimLen(_animHideName, CompleteOutAnimation).Forget();
        }

        #region 奖励逻辑

        /// <summary>从配置加载当前关卡奖励</summary>
        private void LoadRewardData()
        {
            _rewardMap = new Dictionary<int, int>();
            _hasClaimedReward = false;

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

            // 初始化奖励Widget
            RefreshRewardWidgets();
        }

        /// <summary>刷新奖励Widget显示</summary>
        private void RefreshRewardWidgets()
        {
            var tbItem = ConfigSystem.Instance.Tables.TbItem;

            // 金币
            if (_rewardMap.TryGetValue(ItemId.Coin, out int coinCount) && coinCount > 0)
            {
                var itemCfg = tbItem?.GetOrDefault(ItemId.Coin);
                if (itemCfg != null && _rewardItemCoinWidget != null)
                {
                    _rewardItemCoinWidget.Visible = true;
                    var sprite = string.IsNullOrEmpty(itemCfg.ResIcon) ? null : GameModule.Resource.LoadAsset<Sprite>(itemCfg.ResIcon);
                    _rewardItemCoinWidget.SetReward(sprite, coinCount);
                }
            }
            else if (_rewardItemCoinWidget != null)
            {
                _rewardItemCoinWidget.Visible = false;
            }

            // 提示道具
            if (_rewardMap.TryGetValue(ItemId.TipProp, out int tipCount) && tipCount > 0)
            {
                var itemCfg = tbItem?.GetOrDefault(ItemId.TipProp);
                if (itemCfg != null && _rewardItemTipPropWidget != null)
                {
                    _rewardItemTipPropWidget.Visible = true;
                    var sprite = string.IsNullOrEmpty(itemCfg.ResIcon) ? null : GameModule.Resource.LoadAsset<Sprite>(itemCfg.ResIcon);
                    _rewardItemTipPropWidget.SetReward(sprite, tipCount);
                }
            }
            else if (_rewardItemTipPropWidget != null)
            {
                _rewardItemTipPropWidget.Visible = false;
            }

            // 没有奖励则隐藏 rewardRoot
            if (_rewardRoot != null)
            {
                _rewardRoot.gameObject.SetActive(_rewardMap.Count > 0);
            }
        }

        /// <summary>播放奖励展示动画</summary>
        private void PlayRewardShowAnim()
        {
            float delay = 0f;
            if (_rewardItemCoinWidget != null && _rewardItemCoinWidget.Visible)
            {
                _rewardItemCoinWidget.PlayShowAnim(delay);
                delay += 0.2f;
            }
            if (_rewardItemTipPropWidget != null && _rewardItemTipPropWidget.Visible)
            {
                _rewardItemTipPropWidget.PlayShowAnim(delay);
            }
        }

        /// <summary>领取奖励：发道具 + 播放飞行动画</summary>
        private void ClaimReward(System.Action onComplete)
        {
            if (_hasClaimedReward || _rewardMap == null || _rewardMap.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _hasClaimedReward = true;
            SetButtonsInteractable(false);

            int coinCount = _rewardMap.TryGetValue(ItemId.Coin, out int c) ? c : 0;
            int tipCount = _rewardMap.TryGetValue(ItemId.TipProp, out int t) ? t : 0;

            // 先发道具到数据层
            if (coinCount > 0) PropDefine.AddCoin(coinCount);
            if (tipCount > 0) PropDefine.AddTip(tipCount);

            // 播放特效流程
            DoClaimAnimations(coinCount, tipCount, onComplete).Forget();
        }

        private async UniTaskVoid DoClaimAnimations(int coinCount, int tipCount, System.Action onComplete)
        {
            bool coinDone = coinCount <= 0;
            bool tipDone = tipCount <= 0;

            void CheckAllDone()
            {
                if (coinDone && tipDone)
                {
                    onComplete?.Invoke();
                }
            }

            // 金币飞行动画：使用 EffectHelper.FlyCoin，内部会自动更新顶部金币栏
            if (coinCount > 0 && _rewardItemCoinWidget != null)
            {
                GameEvent.Send(EventDefine.Event_UITopCoinAddAnim, coinCount);
                _rewardItemCoinWidget.PlayFlyAnim(0.6f, () =>
                {
                    coinDone = true;
                    CheckAllDone();
                });
            }

            // 提示道具飞行动画：上移 + 渐隐
            if (tipCount > 0 && _rewardItemTipPropWidget != null)
            {
                _rewardItemTipPropWidget.PlayFlyAnim(0.6f, () =>
                {
                    tipDone = true;
                    CheckAllDone();
                });
            }

            // 等待所有动画完成
            await UniTask.WaitWhile(() => !coinDone || !tipDone);

            // 隐藏 rewardRoot
            if (_rewardRoot != null)
                _rewardRoot.gameObject.SetActive(false);
        }

        #endregion

        #region 按钮交互

        private void SetButtonsInteractable(bool interactable)
        {
            _btnNext.Interactable = interactable;
            _btnHome.Interactable = interactable;
        }

        private void OnBtnNextClick()
        {
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
        }
    }
}
