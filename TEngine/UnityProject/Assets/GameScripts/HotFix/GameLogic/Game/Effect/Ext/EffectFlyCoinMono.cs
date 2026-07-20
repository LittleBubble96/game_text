using System;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameLogic.Ext
{
    /// <summary>
    /// 飞金币特效。金币从起始位置，先随机小偏移→停留随机间隔→曲线飞向目标。
    /// 通过 Args 传入 FlyCoinArgs 参数。
    /// </summary>
    public class EffectFlyCoinMono : EffectMono
    {
        private Tween _tween;

        /// <summary>
        /// 随机小位移范围。
        /// </summary>
        public float scatterRadius = 60f;

        /// <summary>
        /// 小位移动画时长。
        /// </summary>
        public float scatterDuration = 0.15f;

        /// <summary>
        /// 停留间隔范围（秒）。
        /// </summary>
        public float stayDelayMin = 0.05f;
        public float stayDelayMax = 0.2f;

        /// <summary>
        /// 飞行到目标时长。
        /// </summary>
        public float flyDuration = 0.5f;

        /// <summary>
        /// 曲线高度系数。
        /// </summary>
        public float arcHeight = 100f;

        /// <summary>
        /// 飞行缓动类型。
        /// </summary>
        public Ease flyEase = Ease.InOutQuad;

        /// <summary>
        /// 是否在飞行过程中缩放缩小。
        /// </summary>
        public bool scaleDownOnFly = true;

        private Vector3 _startPos;
        private Vector3 _targetPos;

        public override void PlayEffect()
        {
            base.PlayEffect();

            // 从多参 CommonArgs 解析：Arg1=起始位置, Arg2=目标位置, Arg3=序号, Arg4=总数, Arg5=到达回调
            if (!(Args is CommonArgs<Vector3, Vector3, int, int, Action> flyArgs))
            {
                DestroySelf();
                return;
            }

            _startPos = flyArgs.Arg1;
            _targetPos = flyArgs.Arg2;
            int index = flyArgs.Arg3;
            int total = flyArgs.Arg4;
            Action onArrived = flyArgs.Arg5;

            transform.position = _startPos;

            // 随机一个方向做小位移
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(scatterRadius * 0.5f, scatterRadius);
            Vector3 scatterOffset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
                0f);
            Vector3 scatterPos = _startPos + scatterOffset;

            float stayDelay = Random.Range(stayDelayMin, stayDelayMax);

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOMove(scatterPos, scatterDuration).SetEase(Ease.OutQuad));
            seq.AppendInterval(stayDelay);

            // 飞行阶段：曲线飞向目标
            Vector3 midPoint = (_startPos + _targetPos) * 0.5f + Vector3.up * arcHeight;

            Vector3[] path = { _startPos, midPoint, _targetPos };
            // 计算飞行阶段实际起点的路径（从 scatterPos 开始）
            Vector3[] flyPath = { scatterPos, midPoint, _targetPos };

            seq.Append(transform.DOPath(flyPath, flyDuration, PathType.CatmullRom).SetEase(flyEase));

            if (scaleDownOnFly)
            {
                seq.Join(transform.DOScale(0.3f, flyDuration).SetEase(Ease.InQuad));
            }

            seq.OnComplete(() =>
            {
                onArrived?.Invoke();
                DestroySelf();
            });

            _tween = seq;
        }

        public override void StopEffect()
        {
            _tween?.Kill();
            _tween = null;
            base.StopEffect();
        }
    }
}
