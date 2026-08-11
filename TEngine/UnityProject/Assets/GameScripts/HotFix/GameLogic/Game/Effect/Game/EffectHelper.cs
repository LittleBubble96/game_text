using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 特效辅助类。提供飞金币等便捷特效入口。
    /// </summary>
    public static class EffectHelper
    {
        /// <summary>UITop 当前显示的金币数（用于递增动画的起始值）。</summary>
        private static int _currentCoinDisplay;

        /// <summary>
        /// 飞金币特效：从起始位置飞到顶部金币栏。
        /// 需要先通过 EffectTargetRegistry.Register(EffectTargetRegistry.Key.TopCoin, xxx) 注册目标位置。
        /// 飞行结束后用 DOTween 在 0.5s 内递增 UITop 金币数。
        /// 递增数量 = totalCoins / coinCount；不能整除时只加 1。
        /// </summary>
        /// <param name="effectName">金币特效资源名称。</param>
        /// <param name="startPos">起始位置（世界坐标）。</param>
        /// <param name="totalCoins">总金币数（用于计算每段金币数和递增数量）。</param>
        /// <param name="parent">父节点（通常为UI Canvas）。</param>
        /// <param name="onAllArrived">全部飞完后回调。</param>
        public static void FlyCoin(string effectName, Vector3 startPos, int totalCoins, Transform parent, Action onAllArrived = null)
        {
            if (totalCoins <= 0) return;

            Vector3 targetPos = EffectTargetRegistry.GetPosition(EffectTargetRegistry.Key.TopCoin);
            if (targetPos == Vector3.zero)
            {
                Log.Warning("[EffectHelper] FlyCoin failed: target 'TopCoin' not registered.");
                return;
            }

            int coinCount = CalcCoinCount(totalCoins);
            DoFlyCoin(effectName, startPos, targetPos, coinCount, totalCoins, parent, onAllArrived).Forget();
        }

        /// <summary>
        /// 飞金币特效（指定目标位置）。
        /// 飞行结束后用 DOTween 在 0.5s 内递增 UITop 金币数。
        /// 递增数量 = totalCoins / coinCount；不能整除时只加 1。
        /// </summary>
        public static void FlyCoinTo(string effectName, Vector3 startPos, Vector3 targetPos, int totalCoins, Transform parent, Action onAllArrived = null)
        {
            if (totalCoins <= 0) return;

            int coinCount = CalcCoinCount(totalCoins);
            DoFlyCoin(effectName, startPos, targetPos, coinCount, totalCoins, parent, onAllArrived).Forget();
        }

        /// <summary>
        /// 根据总金币数计算每段飞行金币数：大于20则/10，否则/5，最多10个，至少1个。
        /// </summary>
        private static int CalcCoinCount(int totalCoins)
        {
            int count = totalCoins > 20 ? totalCoins / 10 : totalCoins / 5;
            count = Mathf.Min(count, 10);
            count = Mathf.Max(count, 1);
            return count;
        }

        private static async UniTaskVoid DoFlyCoin(string effectName, Vector3 startPos, Vector3 targetPos, int coinCount, int totalCoins, Transform parent, Action onAllArrived)
        {
            int arrivedCount = 0;

            for (int i = 0; i < coinCount; i++)
            {
                int index = i;
                var args = CommonArgs.Create(
                    startPos,
                    targetPos,
                    index,
                    coinCount,
                    (Action)(() =>
                    {
                        arrivedCount++;
                        if (arrivedCount >= coinCount)
                        {
                            // 本段飞行结束，递增 UITop 金币数
                            int increment = (totalCoins % coinCount == 0)
                                ? totalCoins / coinCount
                                : 1;

                            AnimateCoinIncrement(increment);

                            onAllArrived?.Invoke();
                        }
                    })
                );

                EffectManager.Instance.PlayEffectAsync(effectName, parent, args).Forget();

                // 每个金币间隔一小段时间发射
                await UniTask.Delay(TimeSpan.FromSeconds(0.03));
            }
        }

        /// <summary>
        /// DOTween 递增 UITop 金币显示，0.5s 内从当前值滚动到目标值。
        /// </summary>
        private static void AnimateCoinIncrement(int increment)
        {
            int startValue = _currentCoinDisplay;
            int targetValue = startValue + increment;

            DOTween.To(
                () => startValue,
                val =>
                {
                    int displayVal = Mathf.RoundToInt(val);
                    if (displayVal != _currentCoinDisplay)
                    {
                        _currentCoinDisplay = displayVal;
                        GameEvent.Send(EventDefine.Event_UITopCoinUpdate, displayVal);
                    }
                },
                targetValue,
                0.5f
            ).OnComplete(() =>
            {
                _currentCoinDisplay = targetValue;
                GameEvent.Send(EventDefine.Event_UITopCoinUpdate, targetValue);
            });
        }
    }
}
