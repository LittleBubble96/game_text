using System;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 特效组件，挂载在特效GameObject上，支持自销毁和外部销毁。
    /// 子类可重写 PlayEffect/StopEffect 实现自定义逻辑。
    /// </summary>
    public class EffectMono : MonoBehaviour
    {
        public uint EffectId { get; set; }

        /// <summary>
        /// 特效资源名称（即资源路径）。
        /// </summary>
        public string EffectName { get; set; }

        /// <summary>
        /// 特效动态参数。
        /// </summary>
        public CommonArgs Args { get; set; }

        /// <summary>
        /// 销毁完成回调。
        /// </summary>
        public event Action<EffectMono> OnDestroyed;

        /// <summary>
        /// 特效播放时调用（由EffectManager触发）。子类可重写。
        /// </summary>
        public virtual void PlayEffect() { }

        /// <summary>
        /// 特效停止时调用（由EffectManager触发）。子类可重写。
        /// </summary>
        public virtual void StopEffect() { }

        /// <summary>
        /// 自身销毁（回收到对象池）。
        /// </summary>
        public void DestroySelf()
        {
            if (string.IsNullOrEmpty(EffectName)) return;

            StopEffect();
            OnDestroyed?.Invoke(this);
            OnDestroyed = null;

            EffectManager.Instance.RecycleEffect(this);
        }

        /// <summary>
        /// 便捷获取参数。
        /// </summary>
        public T GetArg<T>(int index = 0)
        {
            if (Args == null) return default;
            switch (index)
            {
                case 0: return Args is CommonArgs<T> a0 ? a0.Arg1 : default;
                case 1: return Args is CommonArgs<T, T> a1 ? a1.Arg2 : default;
                default: return default;
            }
        }
    }
}