using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameConfig;
using GameLogic.Data;
using GameLogic.GamePlay;
using GameLogic.GamePlay.CorePlay;
using GameLogic.GamePlay.CorePlay.View;
using GameLogic.View;
using TEngine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameLogic
{
    /// <summary>
    /// 游戏总管理器 —— 通过 IGamePlay 接口编排玩法，方便扩展
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        private CorePlayView _corePlayView;

        // ================ 内部模块 ================

        private LevelDataConfigParse _levelConfig;
        private GameCacheManager _cacheManager;

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
        }

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

            Log.Info($"[GameManager] 游戏通关! 关卡: {levelId}");
            // 弹出结算界面（奖励仍按已通关的 levelId 查配置，用于展示数量）
            GameModule.UI.ShowUIAsync<UIFinish>(levelId);
        }

        /// <summary>
        /// 按 levelId 查关卡奖励配置并发放到数据层（金币/提示道具）。
        /// 仅在通关时调用一次，与推进存档一起完成；结算界面只读配置用于展示。
        /// </summary>
        private void GrantLevelReward(int levelId)
        {
            var rewardMap = GetLevelRewardMap(levelId);
            if (rewardMap == null || rewardMap.Count == 0) return;

            if (rewardMap.TryGetValue(ItemId.Coin, out int coinCount) && coinCount > 0)
                PropDefine.AddCoin(coinCount);
            if (rewardMap.TryGetValue(ItemId.TipProp, out int tipCount) && tipCount > 0)
                PropDefine.AddTip(tipCount);
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

        public void ResetProgress()
        {
            _cacheManager.DeleteAll();
            CurrentGamePlay.LoadLevel(1);
            _corePlayView?.ClearAllHighlights();
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