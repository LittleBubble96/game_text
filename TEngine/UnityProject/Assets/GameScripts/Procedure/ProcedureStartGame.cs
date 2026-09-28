using Launcher;
using TEngine;
using UnityEngine;

namespace Procedure
{
    public class ProcedureStartGame : ProcedureBase
    {
        public override bool UseNativeDialog { get; }

        private const string GameStartSuccessEvent = "GameStartSuccessEvent";
        private const float SimulatedDuration = 2f;
        private const float FastFinishDuration = 0.15f;
        private LoadUpdateUI _loadingUI;
        private float _enteredAt;
        private float _readyAt;
        private float _readyProgress;
        private float _finishDuration;
        private bool _active;
        private bool _gameReady;
        private bool _finished;
        private int _fullProgressFrame;

        protected override void OnEnter(IFsm<IProcedureModule> procedureOwner)
        {
            base.OnEnter(procedureOwner);
            _enteredAt = Time.realtimeSinceStartup;
            _gameReady = false;
            _finished = false;
            _active = true;
            _fullProgressFrame = -1;
            LauncherMgr.ShowUI<LoadUpdateUI>(string.Format(LoadText.Instance.Label_Load_Load_Progress, 0));
            _loadingUI = LauncherMgr.GetActiveUI<LoadUpdateUI>();
            GameEvent.AddEventListener(GameStartSuccessEvent, OnGameStartSuccessEvent);
        }

        protected override void OnUpdate(IFsm<IProcedureModule> procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            if (!_active || _finished) return;

            // 使用真实时间，不受 timeScale 影响；满进度不代表初始化已完成。
            float now = Time.realtimeSinceStartup;
            float progress = Mathf.Clamp01((now - _enteredAt) / SimulatedDuration);
            if (_gameReady)
            {
                float t = _finishDuration <= 0f ? 1f : Mathf.Clamp01((now - _readyAt) / _finishDuration);
                progress = Mathf.Lerp(_readyProgress, 1f, t);
            }
            _loadingUI?.RefreshStartupProgress(progress);
            if (progress < 1f) return;

            // 至少展示一帧满进度，再移除启动遮罩。
            if (_fullProgressFrame < 0)
            {
                _fullProgressFrame = Time.frameCount;
                return;
            }
            if (!_gameReady || Time.frameCount <= _fullProgressFrame) return;
            _finished = true;
            GameEvent.RemoveEventListener(GameStartSuccessEvent, OnGameStartSuccessEvent);
            LauncherMgr.HideAllUI(0f);
        }

        private void OnGameStartSuccessEvent()
        {
            if (!_active || _gameReady || _finished) return;
            _gameReady = true;
            _readyAt = Time.realtimeSinceStartup;
            _readyProgress = Mathf.Clamp01((_readyAt - _enteredAt) / SimulatedDuration);
            // 提前完成时加速补满；接近两秒时不比原进度更慢。
            _finishDuration = Mathf.Min(FastFinishDuration, (1f - _readyProgress) * SimulatedDuration);
        }

        protected override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
        {
            _active = false;
            _loadingUI = null;
            GameEvent.RemoveEventListener(GameStartSuccessEvent, OnGameStartSuccessEvent);
            base.OnLeave(procedureOwner, isShutdown);
        }
    }
}
