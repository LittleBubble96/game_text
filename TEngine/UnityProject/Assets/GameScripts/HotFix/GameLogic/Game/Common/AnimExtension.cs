using System;
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
        
        public static async UniTaskVoid PlayAnimWithDelayAnimLen(this Animation animation , string animName, Action call)
        {
            float len = animation.GetClip(animName).length;
            animation.Play(animName);
            await UniTask.WaitForSeconds(len);
            call?.Invoke();
        }
    }
}