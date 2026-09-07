using System;
using Cysharp.Threading.Tasks;
using Launcher;
using TEngine;

namespace Procedure
{
    public class ProcedureStartGame : ProcedureBase
    {
        public override bool UseNativeDialog { get; }

        private const string GameStartSuccessEvent = "GameStartSuccessEvent";

        protected override void OnEnter(IFsm<IProcedureModule> procedureOwner)
        {
            base.OnEnter(procedureOwner);
            LauncherMgr.GetActiveUI<LoadUpdateUI>().HideBar();
            GameEvent.AddEventListener(GameStartSuccessEvent, OnGameStartSuccessEvent);
        }

        private void OnGameStartSuccessEvent()
        {
            StartGame().Forget();
        }

        private async UniTaskVoid StartGame()
        {
            await UniTask.Yield();
            LauncherMgr.HideAllUI();
        }
    }
}