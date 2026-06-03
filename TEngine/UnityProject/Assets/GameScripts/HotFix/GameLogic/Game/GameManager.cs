using GameLogic.Data;
using GameLogic.GamePlay;
using GameLogic.GamePlay.CorePlay;
using GameLogic.GamePlay.CorePlay.View;
using TEngine;
using UnityEngine;

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
        private GameRestoreDataManager _restoreDataManager;

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
            BindEvents();
        }

        private void BindEvents()
        {
            Utility.Unity.AddOnApplicationPauseListener(OnApplicationPause);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveGameProgress();
            }
        }
        
        // ================ 初始化 ================

        private void InitModules()
        {
            _levelConfig = new LevelDataConfigParse();
            _levelConfig.LoadAllLevels();

            _restoreDataManager = new GameRestoreDataManager();

            _corePlayGamePlay = new CorePlayGamePlay();
            _corePlayGamePlay.Initialize(_levelConfig);
            CurrentGamePlay = _corePlayGamePlay;

            // 绑定通关事件 → 自动存档
            CurrentGamePlay.OnLevelCompleted += GameEnd;

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

            _restoreDataManager.Load();

            int startLevelIndex = _restoreDataManager.CorePlayRestoreData.SaveData?.currentLevelIndex ?? 0;
            if (startLevelIndex >= _levelConfig.LevelCount)
                startLevelIndex = 0;

            var restoredAnswers = _restoreDataManager.CorePlayRestoreData.GetFoundAnswers(startLevelIndex);

            // 先初始化视图，再加载关卡（确保视图能响应 OnLevelLoaded）
            if (_corePlayView == null)
            {
                _corePlayView = GenerateCorePlayView();
            }
            _corePlayView.Initialize(CurrentGamePlay, _levelConfig);
            
            _corePlayGamePlay.LoadLevel(startLevelIndex, restoredAnswers);
            CurrentGamePlay.StartGame();

            Debug.Log($"[GameManager] CorePlay 启动，当前关卡: {startLevelIndex}");
            GameModule.UI.ShowUI<UICorePlay>();
        }
        
        private CorePlayView GenerateCorePlayView()
        {
            GameObject viewGo = new GameObject("CorePlayView");
            viewGo.transform.position = Vector3.zero;
            viewGo.transform.localScale = Vector3.one;
            var view = viewGo.AddComponent<CorePlayView>();
            return view;
        }

        // ================ 通关处理 ================
        private void GameEnd(int levelIndex)
        {
            OnLevelCompletedHandler(levelIndex);
            Debug.Log("[GameManager] 游戏结束!");
        }

        private void OnLevelCompletedHandler(int levelIndex)
        {
            SaveGameProgress();
        }

        public void OnCorePlayLevelCompleted(int levelIndex)
        {
            SaveGameProgress();
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

            var restoredAnswers = _restoreDataManager.CorePlayRestoreData.GetFoundAnswers(nextLevel);
            _corePlayGamePlay.LoadLevel(nextLevel, restoredAnswers);
        }

        // ================ 存档管理 ================

        public void SaveGameProgress()
        {
            if (_corePlayGamePlay == null || _restoreDataManager == null) return;

            _corePlayGamePlay.ApplyToRestore(_restoreDataManager.CorePlayRestoreData);
            _restoreDataManager.Save();
        }

        public void ResetProgress()
        {
            _restoreDataManager.DeleteSaveFile();
            CurrentGamePlay.LoadLevel(0);
            _corePlayView?.ClearAllHighlights();
        }

        // ================ 公共接口 ================

        public LevelDataConfigParse LevelConfig => _levelConfig;
        public GameRestoreDataManager RestoreDataManager => _restoreDataManager;
    }
}