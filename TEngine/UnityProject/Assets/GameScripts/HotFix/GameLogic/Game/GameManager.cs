using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameConfig;
using GameLogic.Data;
using GameLogic.GamePlay;
using GameLogic.GamePlay.CorePlay;
using GameLogic.GamePlay.CorePlay.View;
using GameLogic.View;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using YooAsset;
using Object = UnityEngine.Object;

namespace GameLogic
{
    /// <summary>
    /// 游戏总管理器 —— 通过 IGamePlay 接口编排玩法，方便扩展
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        private CorePlayView _corePlayView;
        // 仅持有资源引用，不提前实例化窗口，避免触发引导/结算的业务回调。
        private readonly Dictionary<string, AssetHandle> _uiPreloadHandles = new Dictionary<string, AssetHandle>();
        private bool _released;
        private const int FinishDelayMilliseconds = 700;
        private CancellationTokenSource _finishDelayCancellation;
        private readonly LevelCompletionReward _completionReward = new LevelCompletionReward();
        public int CompletionRewardVersion => _completionReward.Version;

        public bool CanClaimDoubleReward(int levelId, int version) => _completionReward.CanClaim(levelId, version);
        public bool IsDoubleRewardClaimed(int levelId, int version) => _completionReward.HasClaimed(levelId, version);

        /// <summary>只由完整观看广告的结算回调调用，奖励使用通关时的快照。</summary>
        public bool TryClaimDoubleReward(int levelId, int version, bool adCompleted)
        {
            if (!_completionReward.TryClaim(levelId, version, adCompleted, out var rewards)) return false;
            GrantRewardMap(levelId, rewards, "level_reward_double_ad");
            return true;
        }

        // ================ 内部模块 ================

        private LevelDataConfigParse _levelConfig;
        private GameCacheManager _cacheManager;
        public bool FirstLevelGuideCompleted => _cacheManager?.CacheData?.firstLevelGuideCompleted ?? false;

        public void CompleteFirstLevelGuide()
        {
            if (_cacheManager?.CacheData == null) return;
            _cacheManager.CacheData.firstLevelGuideCompleted = true;
            _cacheManager.Save();
        }

        /// <summary>当前玩法（通过接口暴露，扩展时替换实现即可）</summary>
        public IGamePlay CurrentGamePlay { get; private set; }
        
        //当前视图
        public CorePlayView CurrentView => _corePlayView;

        // CorePlay 专用引用（存档/恢复等类型相关操作）
        private CorePlayGamePlay _corePlayGamePlay;

        // ================ Unity 生命周期 ================

        protected override void OnInit()
        {
            base.OnInit();
            CreateAppPauseBridge();
        }
        
        
        public async UniTask InitMgr()
        {
            await InitModules();
            _cacheManager?.Load();

            // 从缓存同步初始关卡ID到 gameplay（避免 _currentLevelId 长期为 -1）
            int savedLevel = _cacheManager?.CorePlayRestore?.SaveData?.currentLevelId ?? 1;
            _corePlayGamePlay?.InitLevelId(savedLevel);
            PreloadGameplayUI(savedLevel);
            var initialLevel = _cacheManager.CorePlayRestore.GetCachedLevelData() ??
                _levelConfig.GetLevelDataByLevelId(savedLevel) ?? _levelConfig.GetLevelDataByLevelId(1);
            if (initialLevel != null)
            {
                // Loading 阶段读取首关分片并预热答案槽。
                await UniTask.WhenAll(_levelConfig.GetGraphicDataAsync(initialLevel.baseCharacter),
                    GameSlotView.PrewarmAsync(initialLevel));
            }
        }

        private void PreloadUIAsset(string location)
        {
            if (_released) return;
            if (_uiPreloadHandles.TryGetValue(location, out var existing))
            {
                if (existing.Status != EOperationStatus.Failed) return;
                existing.Dispose();
                _uiPreloadHandles.Remove(location);
            }
            try
            {
                _uiPreloadHandles.Add(location, GameModule.Resource.LoadAssetAsyncHandle<GameObject>(location));
            }
            catch (Exception exception)
            {
                // 预加载是可选优化；失败时仍由正式打开窗口的流程加载。
                Log.Warning($"[GameManager] UI 预加载未启动: {location}, {exception.Message}");
            }
        }

        private void PreloadGameplayUI(int levelId)
        {
            PreloadUIAsset("UICorePlay");
            PreloadUIAsset("GameViewRoot");
            if (levelId == 1 && !FirstLevelGuideCompleted) PreloadUIAsset("UIGuide");
        }

        /// <summary>首页就绪后预读结算资源，不阻塞启动，也不触发结算埋点或奖励逻辑。</summary>
        public void PreloadFinishUI() => PreloadUIAsset("UIFinish");

        /// <summary>创建 MonoBehaviour 桥接，监听 Unity OnApplicationPause 并转发</summary>
        private void CreateAppPauseBridge()
        {
            var go = new GameObject("[GameManagerBridge]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            var bridge = go.AddComponent<AppPauseBridge>();
            bridge.OnAppPause += HandleAppPause;
            bridge.OnAppQuit += HandleAppQuit;
        }

        private void HandleAppPause(bool pauseStatus)
        {
            BiMgr.SetPaused(pauseStatus);
            if (pauseStatus)
            {
                SaveGameProgress();
            }
        }

        private void HandleAppQuit()
        {
            SaveGameProgress();
        }

        /// <summary>Monobehaviour 桥接：将 Unity OnApplicationPause / OnApplicationQuit 转发给 GameManager</summary>
        private class AppPauseBridge : MonoBehaviour
        {
            public event Action<bool> OnAppPause;
            public event Action OnAppQuit;

            private void OnApplicationQuit()
            {
                OnAppQuit?.Invoke();
            }

            private void OnApplicationPause(bool pauseStatus)
            {
                OnAppPause?.Invoke(pauseStatus);
            }
        }
        
        // ================ 初始化 ================

        private async UniTask InitModules()
        {
            _levelConfig = new LevelDataConfigParse();
            await _levelConfig.LoadAllLevels();

            _cacheManager = new GameCacheManager();

            _corePlayGamePlay = new CorePlayGamePlay();
            _corePlayGamePlay.Initialize(_levelConfig);
            CurrentGamePlay = _corePlayGamePlay;

            // 预加载笔画材质模板（loading 阶段异步，避免运行时 Draw 卡帧 + shader 随资源进包）
            await DrawCharacter.PreloadStrokeMaterialAsync();

            // 绑定全局通关事件 → 自动存档 + 弹出结算
            GameEvent.AddEventListener<int>(EventDefine.Event_LevelCompleted, OnLevelCompleted);

            Log.Info("[GameManager] 所有模块初始化完成");
        }

        // ================ 启动玩法 ================

        public async UniTask StartGame()
        {
            await StartCorePlay();
        }

        private async UniTask StartCorePlay()
        {
            if (_levelConfig.LevelCount == 0)
            {
                Log.Error("[GameManager] 没有可用的关卡数据");
                return;
            }

            int startLevelId = _cacheManager.CorePlayRestore.SaveData?.currentLevelId ?? 1;
            // 防御：检查关卡ID是否有效
            if (_levelConfig.GetLevelNameByLevelId(startLevelId) == null)
                startLevelId = 1;

            // 重置存档、GM 跳关、首次预加载失败时也走同一入口。
            PreloadGameplayUI(startLevelId);
            PreloadFinishUI();

            var restoredAnswers = _cacheManager.CorePlayRestore.GetFoundAnswers();
            var cachedLevelData = _cacheManager.CorePlayRestore.GetCachedLevelData();

            // 先初始化视图，再加载关卡（确保视图能响应 OnLevelLoaded）
            if (_corePlayView == null)
            {
                _corePlayView = await GenerateCorePlayViewAsync();
            }
            _corePlayView.Initialize(CurrentGamePlay, _levelConfig);
            _corePlayView.OnEnterGameAnim();
            
            _corePlayGamePlay.LoadLevel(startLevelId, restoredAnswers, cachedLevelData);
            CurrentGamePlay.StartGame();

            Log.Info($"[GameManager] CorePlay 启动，当前关卡: {startLevelId}");
            GameModule.UI.ShowUIAsync<UICorePlay>();
            GameModule.UI.CloseUI<UIHome>();
        }
        
        private async UniTask<CorePlayView> GenerateCorePlayViewAsync()
        {
            GameObject viewGo = new GameObject("CorePlayView");
            viewGo.transform.position = Vector3.zero;
            viewGo.transform.localScale = Vector3.one;
            var view = viewGo.AddComponent<CorePlayView>();
            await view.OnCreateAsync();
            return view;
        }

        // ================ 通关处理 ================
        private void OnLevelCompleted(int levelId)
        {
            // 关卡通关的唯一推进点：把存档推进到下一关（清空答案进度与关卡快照），
            // 之后无论玩家点“下一关”回到游戏，还是强退重进，都会从下一关开始。
            // 这一步集中处理，避免各处分散打补丁；gameplay 层无需感知“推进”。
            AdvanceSaveToNextLevel(levelId);

            // 通关即发放奖励数据（金币/提示道具），强退也不丢、不重复领；
            // 结算界面点击按钮只播奖励飞行动画表现，不再改数据。
            GrantLevelReward(levelId);

            if (!_corePlayGamePlay.HasNextLevel())
            {
                // 每个内容版本仅记录一次；旧存档缺省为 null。
                string contentVersion = Application.version + ":" + _levelConfig.MaxLevelId;
                var cache = _cacheManager.CacheData;
                if (cache.biAllLevelsCompletedVersion != contentVersion)
                {
                    cache.biAllLevelsCompletedVersion = contentVersion;
                    _cacheManager.Save();
                    BiMgr.AllLevelsCompleted(levelId, contentVersion);
                }
            }

            Log.Info($"[GameManager] 游戏通关! 关卡: {levelId}");
            // 数据立即保存，结算展示稍后进行，给最后一个答案留出入槽动画时间。
            ShowFinishAfterAnimationAsync(levelId).Forget();
        }

        private async UniTask ShowFinishAfterAnimationAsync(int levelId)
        {
            if (_finishDelayCancellation != null) return;
            var cancellation = _finishDelayCancellation = new CancellationTokenSource();
            var eventSystem = EventSystem.current;
            bool restoreEventSystem = eventSystem != null && eventSystem.enabled;
            using (UIInteractionLock.Acquire())
            {
                // 同时拦截普通 Button 等未接入交互锁的 UI 输入。
                if (restoreEventSystem) eventSystem.enabled = false;
                try
                {
                    await UniTask.Delay(FinishDelayMilliseconds, ignoreTimeScale: true,
                        cancellationToken: cancellation.Token);
                    await GameModule.UI.ShowUIAsyncAwait<UIFinish>(levelId);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // 返回主页或释放管理器时取消等待，不再弹出旧关卡结算。
                }
                finally
                {
                    if (restoreEventSystem && eventSystem != null) eventSystem.enabled = true;
                    if (ReferenceEquals(_finishDelayCancellation, cancellation))
                        _finishDelayCancellation = null;
                    cancellation.Dispose();
                }
            }
        }

        /// <summary>
        /// 按 levelId 查关卡奖励配置并发放到数据层（金币及各类道具）。
        /// 仅在通关时调用一次，与推进存档一起完成；结算界面只读配置用于展示。
        /// </summary>
        private void GrantLevelReward(int levelId)
        {
            var rewardMap = GetLevelRewardMap(levelId);
            _completionReward.Begin(levelId, rewardMap);
            GrantRewardMap(levelId, rewardMap, "level_reward");
        }

        private void GrantRewardMap(int levelId, Dictionary<int, int> rewardMap, string source)
        {
            if (rewardMap == null || rewardMap.Count == 0) return;

            foreach (var reward in rewardMap)
            {
                if (reward.Value <= 0) continue;
                switch (reward.Key)
                {
                    case ItemId.Coin:
                        PropDefine.AddCoin(reward.Value, source);
                        break;
                    case ItemId.TipProp:
                        PropDefine.AddTip(reward.Value, source);
                        break;
                    case ItemId.ResetProp:
                        PropDefine.AddReset(reward.Value, source);
                        break;
                    case ItemId.NextProp:
                        PropDefine.AddNext(reward.Value, source);
                        break;
                    default:
                        Log.Error($"[GameManager] 关卡 {levelId} 的奖励道具 {reward.Key} 尚未支持发放");
                        break;
                }
            }
        }

        /// <summary>查询关卡奖励映射（itemId -> 数量），无奖励返回 null</summary>
        private static Dictionary<int, int> GetLevelRewardMap(int levelId)
        {
            var tbLevel = ConfigSystem.Instance.Tables.TbLevel;
            if (tbLevel == null || !tbLevel.DataMap.TryGetValue(levelId, out var confLevel))
                return null;

            int rewardId = confLevel.RewardId;
            if (rewardId <= 0) return null;

            var confReward = ConfigSystem.Instance.Tables.TbReward?.GetOrDefault(rewardId);
            if (confReward?.Rewards == null || confReward.Rewards.Count == 0) return null;

            return new Dictionary<int, int>(confReward.Rewards);
        }

        /// <summary>
        /// 将存档推进到 levelId 的下一关并落盘。
        /// 在 OnLevelCompleted 调用一次即可，强退/回主页时的 SaveGameProgress
        /// 不会重复推进（存档已是下一关，gameplay 进度对存档不可逆覆盖）。
        /// 通关最后一关时存档推进到 MaxLevelId+1（超过最大关），用于判断全通关。
        /// </summary>
        private void AdvanceSaveToNextLevel(int completedLevelId)
        {
            _cacheManager?.AdvanceToNextLevel(completedLevelId);
        }

        /// <summary>加载下一关</summary>
        public void LoadNextCorePlayLevel()
        {
            int nextLevelId = CurrentGamePlay.CurrentLevelId + 1;
            if (_levelConfig.GetLevelNameByLevelId(nextLevelId) == null)
            {
                Log.Info("[GameManager] 已通过所有关卡!");
                return;
            }

            // 下一关没有缓存，直接从配置加载
            _corePlayGamePlay.LoadLevel(nextLevelId);
            GameModule.UI.ShowUIAsync<UICorePlay>();
        }

        /// <summary>返回主界面</summary>
        public void ReturnToHome()
        {
            _finishDelayCancellation?.Cancel();
            BiMgr.LeaveLevelForHome(_corePlayGamePlay.FoundAnswerIndices.Count);
            SaveGameProgress();
            _corePlayView?.ClearAllHighlights();
            _corePlayView?.OnEndGameAnim();
            CurrentGamePlay?.EndGame();
            
            GameModule.UI.CloseUI<UICorePlay>();
            GameModule.UI.CloseUI<UIFinish>();
            GameModule.UI.ShowUIAsync<UIHome>();
            
            Log.Info("[GameManager] 返回主界面");
        }

        // ================ 存档管理 ================

        public void SaveGameProgress()
        {
            if (_corePlayGamePlay == null || _cacheManager == null) return;

            _corePlayGamePlay.ApplyToRestore(_cacheManager.CorePlayRestore);
            _cacheManager.Save();
        }

        /// <summary>GM 跳关：只更新玩法数据和关卡存档，保留 UI 及其他存档。</summary>
        public bool TrySetCorePlayLevel(int levelId)
        {
            if (_corePlayGamePlay == null || _cacheManager == null)
            {
                Log.Warning("[GameManager] 跳关失败：玩法或存档尚未初始化");
                return false;
            }

            if (!_corePlayGamePlay.TrySetLevelData(levelId)) return false;

            // 数据跳关不启动玩法，不能走仅保存运行中关卡的 SaveGameProgress。
            // 用完整的新关卡存档替换旧答案与快照，避免下次进入复用旧关卡。
            _cacheManager.CorePlayRestore.LoadFromData(_corePlayGamePlay.GetSaveData());
            _cacheManager.Save();
            return true;
        }

        public void ResetProgress()
        {
            _cacheManager.DeleteAll();
            CurrentGamePlay.LoadLevel(1);
            _corePlayView?.ClearAllHighlights();
        }

        protected override void OnRelease()
        {
            _released = true;
            foreach (var handle in _uiPreloadHandles.Values) handle.Dispose();
            _uiPreloadHandles.Clear();
            _finishDelayCancellation?.Cancel();
            _levelConfig?.ReleaseGraphicCache();
            base.OnRelease();
        }

        // ================ 公共接口 ================

        public LevelDataConfigParse LevelConfig => _levelConfig;
        public GameCacheManager CacheManager => _cacheManager;

        /// <summary>
        /// 是否已通关所有关卡：存档 currentLevelId 超过最大关卡ID（通关最后一关后推进到 MaxLevelId+1）。
        /// 实时计算、不持久化通关标志——后续更新新增关卡后 MaxLevelId 增大，判断自动失效，可直接继续游戏。
        /// </summary>
        public bool IsAllLevelCompleted
        {
            get
            {
                int cur = _cacheManager?.CorePlayRestore?.SaveData?.currentLevelId ?? 1;
                int max = _levelConfig?.MaxLevelId ?? 0;
                return max > 0 && cur > max;
            }
        }
    }
}
