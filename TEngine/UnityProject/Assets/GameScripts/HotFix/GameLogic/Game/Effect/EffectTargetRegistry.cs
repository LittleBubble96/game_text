using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 特效目标注册中心。通过名称注册/获取 Transform，实现低耦合。
    /// 例如：UITop 注册金币栏位置，飞金币特效通过名称获取目标。
    /// </summary>
    public static class EffectTargetRegistry
    {
        private static readonly Dictionary<string, Transform> _targets = new Dictionary<string, Transform>();

        /// <summary>
        /// 注册一个特效目标点。
        /// </summary>
        public static void Register(string key, Transform target)
        {
            if (string.IsNullOrEmpty(key) || target == null) return;
            _targets[key] = target;
        }

        /// <summary>
        /// 取消注册。
        /// </summary>
        public static void Unregister(string key)
        {
            _targets.Remove(key);
        }

        /// 获取已注册的目标点。
        /// <summary>
        /// </summary>
        public static Transform Get(string key)
        {
            _targets.TryGetValue(key, out var target);
            return target;
        }

        /// <summary>
        /// 获取目标点的世界坐标，未注册返回 Vector3.zero。
        /// </summary>
        public static Vector3 GetPosition(string key)
        {
            var target = Get(key);
            return target != null ? target.position : Vector3.zero;
        }

        /// <summary>
        /// 预定义 key 常量，方便统一管理。
        /// </summary>
        public static class Key
        {
            /// <summary>顶部金币栏位置</summary>
            public const string TopCoin = "TopCoin";
        }
    }
}
