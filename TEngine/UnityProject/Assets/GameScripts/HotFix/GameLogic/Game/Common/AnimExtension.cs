using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameLogic
{
    public static class AnimExtension
    {
        public static async UniTaskVoid PlayAnimWithDelay(this Animation animation , string animName,float delay , Action call)
        {
            animation.Play(animName);
            await UniTask.WaitForSeconds(delay);
            call?.Invoke();
        }
        
        public static async UniTaskVoid PlayAnimWithDelayAnimLen(this Animation animation , string animName, Action call, CancellationToken cancellationToken = default)
        {
            float len = animation.GetClip(animName).length;
            animation.Play(animName);
            if (await UniTask.Delay(TimeSpan.FromSeconds(len), cancellationToken: cancellationToken).SuppressCancellationThrow()) return;
            if (!cancellationToken.IsCancellationRequested) call?.Invoke();
        }
    }
}