using System;
using GameLogic.Data;
using GameLogic.GamePlay;
using GameLogic.GamePlay.CorePlay;
using GameLogic.GamePlay.CorePlay.View;
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
            InitModules();
            CreateAppPauseBridge();
        }

        /// <summary>
        /// Active：在 Singleton.Instance 创建后调用，用于加载缓存使设置数据（语言等）即时可用。
        /// </summary>
        public override void Active()
        {
            base.Active();
            _cacheManager?.Load();

            // 从缓存同步初始关卡索引到 gameplay（避免 _currentLevelIndex 长期为 -1）
            int savedLevel = _cacheManager?.CorePlayRestore?.SaveData?.currentLevelIndex ?? 0;
            _corePlayGamePlay?.InitLevelIndex(savedLevel);
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

        private void InitModules()
        {
            _levelConfig = new LevelDataConfigParse();
            _levelConfig.LoadAllLevels();

            _cacheManager = new GameCacheManager();

            _corePlayGamePlay = new CorePlayGamePlay();
            _corePlayGamePlay.Initialize(_levelConfig);
            CurrentGamePlay = _corePlayGamePlay;

            // 绑定全局通关事件 → 自动存档 + 弹出结算
            GameEvent.AddEventListener<int>(EventDefine.Event_LevelCompleted, OnLevelCompleted);

            Debug.Log("[GameManager] 所有模块初始化完成");
        }

        // ================ 启动玩法 ================

        public void StartGame()
        {
            GameModule.UI.CloseUI<UIHome>();
            StartCorePlay();
        }

        private void StartCorePlay()
        {
            if (_levelConfig.LevelCount == 0)
            {
                Debug.LogError("[GameManager] 没有可用的关卡数据");
                return;
            }

            int startLevelIndex = _cacheManager.CorePlayRestore.SaveData?.currentLevelIndex ?? 0;
            // 防御：兼容旧存档可能残留的 -1（首页退出未开始游戏的情况）
            if (startLevelIndex < 0 || startLevelIndex >= _levelConfig.LevelCount)
                startLevelIndex = 0;

            var restoredAnswers = _cacheManager.CorePlayRestore.GetFoundAnswers(startLevelIndex);

            // 先初始化视图，再加载关卡（确保视图能响应 OnLevelLoaded）
            if (_corePlayView == null)
            {
                _corePlayView = GenerateCorePlayView();
            }
            _corePlayView.Initialize(CurrentGamePlay, _levelConfig);
            _corePlayView.OnEnterGameAnim();
            
            _corePlayGamePlay.LoadLevel(startLevelIndex, restoredAnswers);
            CurrentGamePlay.StartGame();

            Debug.Log($"[GameManager] CorePlay 启动，当前关卡: {startLevelIndex}");
            GameModule.UI.ShowUIAsync<UICorePlay>();
        }
        
        private CorePlayView GenerateCorePlayView()
        {
            GameObject viewGo = new GameObject("CorePlayView");
            viewGo.transform.position = Vector3.zero;
            viewGo.transform.localScale = Vector3.one;
            var view = viewGo.AddComponent<CorePlayView>();
            view.OnCreate();
            return view;
        }

        // ================ 通关处理 ================
        private void OnLevelCompleted(int levelIndex)
        {
            SaveGameProgress();
            Debug.Log($"[GameManager] 游戏通关! 关卡: {levelIndex}");
            // 弹出结算界面
            GameModule.UI.ShowUIAsync<UIFinish>(levelIndex);
        }

        /// <summary>加载下一关</summary>
        public void LoadNextCorePlayLevel()
        {
            int nextLevel = CurrentGamePlay.CurrentLevelIndex + 1;
            if (nextLevel >= _levelConfig.LevelCount)
            {
                Debug.Log("[GameManager] 已通过所有关卡!");
                return;
            }

            var restoredAnswers = _cacheManager.CorePlayRestore.GetFoundAnswers(nextLevel);
            _corePlayGamePlay.LoadLevel(nextLevel, restoredAnswers);
            // 刷新 UI
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
            
            Debug.Log("[GameManager] 返回主界面");
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
            CurrentGamePlay.LoadLevel(0);
            _corePlayView?.ClearAllHighlights();
        }

        // ================ 公共接口 ================

        public LevelDataConfigParse LevelConfig => _levelConfig;
        public GameCacheManager CacheManager => _cacheManager;
    }
}