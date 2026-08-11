using System;
using System.IO;
using Cysharp.Threading.Tasks;
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

            Debug.Log("[GameManager] 所有模块初始化完成");
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
                Debug.LogError("[GameManager] 没有可用的关卡数据");
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

            Debug.Log($"[GameManager] CorePlay 启动，当前关卡: {startLevelId}");
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
            SaveGameProgress();
            Debug.Log($"[GameManager] 游戏通关! 关卡: {levelId}");
            // 弹出结算界面
            GameModule.UI.ShowUIAsync<UIFinish>(levelId);
        }

        /// <summary>加载下一关</summary>
        public void LoadNextCorePlayLevel()
        {
            int nextLevelId = CurrentGamePlay.CurrentLevelId + 1;
            if (_levelConfig.GetLevelNameByLevelId(nextLevelId) == null)
            {
                Debug.Log("[GameManager] 已通过所有关卡!");
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
            CurrentGamePlay.LoadLevel(1);
            _corePlayView?.ClearAllHighlights();
        }

        // ================ 公共接口 ================

        public LevelDataConfigParse LevelConfig => _levelConfig;
        public GameCacheManager CacheManager => _cacheManager;
        
    }
}