using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 特效管理器。EffectName即资源路径，基于GameDataPoolManager对象池。
    /// </summary>
    public class EffectManager : Singleton<EffectManager>
    {
        private uint _generateId = 0;

        /// <summary>
        /// 活跃特效字典。
        /// </summary>
        private readonly Dictionary<uint, EffectMono> _activeEffects = new Dictionary<uint, EffectMono>();

        /// <summary>
        /// 播放特效（异步：对象池冷启动时需加载资源）。
        /// </summary>
        /// <param name="effectName">特效资源名称（即资源路径）。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="args">动态参数CommonArgs，可为null。</param>
        /// <returns>特效ID，0表示失败。</returns>
        public async UniTask<uint> PlayEffectAsync(string effectName, Transform parent, CommonArgs args = null)
        {
            GameObject effect = await GameDataPoolManager.Instance.AllocateGameObjectAsync(effectName, parent);
            if (effect == null)
            {
                Log.Error($"[EffectManager] PlayEffect failed: resource '{effectName}' not found.");
                return 0;
            }

            EffectMono mono = effect.GetOrAddComponent<EffectMono>();
            if (mono == null)
            {
                Log.Error($"[EffectManager] PlayEffect failed: EffectMono component missing on '{effectName}'.");
                return 0;
            }

            _generateId++;
            mono.EffectId = _generateId;
            mono.EffectName = effectName;
            mono.Args = args;
            mono.OnDestroyed += OnEffectDestroyed;

            _activeEffects[_generateId] = mono;
            return _generateId;
        }

        /// <summary>
        /// 外部销毁指定特效（回收到对象池）。
        /// </summary>
        public void StopEffect(uint effectId)
        {
            if (_activeEffects.TryGetValue(effectId, out var mono))
            {
                mono.DestroySelf();
            }
        }

        /// <summary>
        /// 根据ID获取EffectMono。
        /// </summary>
        public EffectMono GetEffect(uint effectId)
        {
            _activeEffects.TryGetValue(effectId, out var mono);
            return mono;
        }

        /// <summary>
        /// 销毁所有活跃特效。
        /// </summary>
        public void StopAllEffects()
        {
            var list = new List<EffectMono>(_activeEffects.Values);
            foreach (var mono in list)
            {
                if (mono != null)
                {
                    mono.DestroySelf();
                }
            }
            _activeEffects.Clear();
        }

        /// <summary>
        /// 回收特效到对象池（由EffectMono.DestroySelf内部调用）。
        /// </summary>
        internal void RecycleEffect(EffectMono mono)
        {
            if (mono == null) return;

            _activeEffects.Remove(mono.EffectId);
            mono.OnDestroyed -= OnEffectDestroyed;
            mono.Args = null;

            GameDataPoolManager.Instance.RecycleGameObject(mono.gameObject, mono.EffectName);
        }

        private void OnEffectDestroyed(EffectMono mono)
        {
            // 回调预留，可扩展
        }

        protected override void OnRelease()
        {
            StopAllEffects();
            base.OnRelease();
        }
    }
}