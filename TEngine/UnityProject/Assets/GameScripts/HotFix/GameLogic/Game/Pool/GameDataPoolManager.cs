
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic;
using TEngine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameLogic
{
    public class PoolObjectItem : ObjectBase
    {
        /// <summary>
        /// 释放对象。
        /// </summary>
        /// <param name="isShutdown">是否是关闭对象池时触发。</param>
        protected override void Release(bool isShutdown)
        {
            // 实例上的 AssetsReference 在 OnDestroy 中归还资源引用；不要重复 UnloadAsset。
            var obj = Target is Component component ? component.gameObject : Target as GameObject;
            if (obj == null) return;
            obj.SetActive(false);
            Object.Destroy(obj);
        }
    
        internal void DiscardUnregistered()
        {
            Release(false);
            MemoryPool.Release(this);
        }

        /// <summary>
        /// 创建Actor对象（异步加载资源，对象池冷启动时使用）。
        /// </summary>
        /// <param name="actorName">对象名称。</param>
        /// <param name="target">对象持有实例。</param>
        /// <returns></returns>
        public static async UniTask<PoolObjectItem> CreateComponentAsync<T>(string resPath, Transform parent = null) where T : MonoBehaviour
        {
            var gameObjectItem = MemoryPool.Acquire<PoolObjectItem>();
            // 先完成一次激活，再移入隐藏池。Unity 对从未激活的对象不保证调用
            // OnDestroy，直接在 inactive 父节点下创建会漏掉 AssetsReference 的归还。
            var itemObj = await GameModule.Resource.LoadGameObjectAsync(resPath);
            if (itemObj == null)
            {
                MemoryPool.Release(gameObjectItem);
                throw new InvalidOperationException($"对象池资源加载失败: {resPath}");
            }
            var target = itemObj.GetOrAddComponent<T>();
            if (parent != null) itemObj.transform.SetParent(parent, false);
            gameObjectItem.Initialize(resPath, target);
            return gameObjectItem;
        }

        public static async UniTask<PoolObjectItem> CreateObjectAsync(string resPath)
        {
            var gameObjectItem = MemoryPool.Acquire<PoolObjectItem>();
            var target = await GameModule.Resource.LoadGameObjectAsync(resPath);
            gameObjectItem.Initialize(resPath, target);
            return gameObjectItem;
        }
        
    }
    
    /// <summary>
    /// 框架objectmanger中存储所有的对象池 这里不做过多处理 只方便使用
    /// </summary>
    public class GameDataPoolManager : Singleton<GameDataPoolManager>
    {
        private Transform _poolTransform;
        private IObjectPoolModule _poolModule;
        private readonly HashSet<string> _warmingPools = new HashSet<string>();
        private readonly HashSet<string> _ownedPools = new HashSet<string>();
        private bool _released;

        protected override void OnInit()
        {
            base.OnInit();
            _poolModule = ModuleSystem.GetModule<IObjectPoolModule>();
            GameObject poolObj = new GameObject("ObjectPool");
            poolObj.SetActive(false);
            _poolTransform = poolObj.transform;
            Object.DontDestroyOnLoad(_poolTransform);
        }

        public override void Active()
        {
            
        }
        
        
        public async UniTask RegisterComponentPoolAsync<T>(string resPath, int capacity = 10) where T : MonoBehaviour
        {
            // 同一个池的并发预热串行化；可补充已存在的池，而非只处理首次创建。
            while (_warmingPools.Contains(resPath)) await UniTask.Yield();
            if (_released) return;
            _warmingPools.Add(resPath);
            try
            {
                var pool = GetAndCreateObjectPool(resPath, capacity);
                pool.Capacity = Math.Max(pool.Capacity, capacity);
                while (pool.Count < capacity)
                {
                    var item = await PoolObjectItem.CreateComponentAsync<T>(resPath, _poolTransform);
                    if (_released || !_poolModule.HasObjectPool<PoolObjectItem>(resPath) ||
                        !ReferenceEquals(pool, _poolModule.GetObjectPool<PoolObjectItem>(resPath)))
                    {
                        item.DiscardUnregistered();
                        return;
                    }
                    pool.Register(item, false);
                    // 每帧最多预热一个对象，避免缓存命中时实例化集中在同一帧。
                    await UniTask.Yield();
                }
            }
            finally { _warmingPools.Remove(resPath); }
        }

        public void UnRegisterComponentPool(string resPath)
        {
            if (_poolModule.HasObjectPool<PoolObjectItem>(resPath))
            {
                _poolModule.DestroyObjectPool<PoolObjectItem>(resPath);
            }
        }
        
        public async UniTask RegisterGameObjectPoolAsync(string resPath, int capacity = 10)
        {
            IObjectPool<PoolObjectItem> _pool = null;
            if (!_poolModule.HasObjectPool<PoolObjectItem>(resPath))
            {
                _pool = _poolModule.CreateSingleSpawnObjectPool<PoolObjectItem>(resPath, capacity);
                _ownedPools.Add(resPath);

                for (int i = 0; i < capacity; i++)
                {
                    var ret = await PoolObjectItem.CreateObjectAsync(resPath);
                    _pool.Register(ret, false);
                    var obj = ret.Target as GameObject;
                    obj.transform.SetParent(_poolTransform);
                }
            }
        }
        
        public void UnRegisterGameObjectPool(string poolName)
        {
            if (_poolModule.HasObjectPool<PoolObjectItem>(poolName))
            {
                _poolModule.DestroyObjectPool<PoolObjectItem>(poolName);
            }
        }

        private IObjectPool<PoolObjectItem> GetAndCreateObjectPool(string typeName, int capacity = 10)
        {
            IObjectPool<PoolObjectItem> _pool;
            if (_poolModule.HasObjectPool<PoolObjectItem>(typeName))
            {
                _pool = _poolModule.GetObjectPool<PoolObjectItem>(typeName);
            }
            else
            {
                _pool = _poolModule.CreateSingleSpawnObjectPool<PoolObjectItem>(typeName, capacity);
                _ownedPools.Add(typeName);
            }

            return _pool;
        }
        
        
        /// <summary>
        /// 组件对象（异步：对象池有可用实例时走 Spawn 同步返回，冷启动时异步加载资源）
        /// </summary>
        /// <param name="resPath"></param>
        /// <param name="parent"></param>
        /// <param name="capacity"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async UniTask<T> AllocateComponentAsync<T>(string resPath, Transform parent, int capacity = 10) where T : MonoBehaviour
        {
            //string typeName = typeof(T).Name;
            PoolObjectItem ret;
            IObjectPool<PoolObjectItem> _pool = GetAndCreateObjectPool(resPath, capacity);

            if (_pool.CanSpawn(resPath))
            {
                ret = _pool.Spawn(resPath);
            }
            else
            {
                ret = await PoolObjectItem.CreateComponentAsync<T>(resPath);
                _pool.Register(ret, true);
            }

            var obj = ret.Target as T;
            obj.transform.SetParent(parent, false);
            return obj;
        }

        /// <summary>
        /// 实体对象（异步：对象池有可用实例时走 Spawn 同步返回，冷启动时异步加载资源）
        /// </summary>
        /// <param name="resPath"></param>
        /// <param name="parent"></param>
        /// <param name="capacity"></param>
        /// <returns></returns>
        public async UniTask<GameObject> AllocateGameObjectAsync(string resPath, Transform parent, int capacity = 10)
        {
            //string typeName = typeof(T).Name;
            PoolObjectItem ret;
            IObjectPool<PoolObjectItem> _pool = GetAndCreateObjectPool(resPath, capacity);

            if (_pool.CanSpawn(resPath))
            {
                ret = _pool.Spawn(resPath);
            }
            else
            {
                ret = await PoolObjectItem.CreateObjectAsync(resPath);
                _pool.Register(ret, true);
            }

            var obj = ret.Target as GameObject;
            obj.transform.SetParent(parent);
            return obj;
        }
        
        public void RecycleGameObject(GameObject obj, string resPath)
        {
            if (_poolModule.HasObjectPool<PoolObjectItem>(resPath))
            {
                var _pool = _poolModule.GetObjectPool<PoolObjectItem>(resPath);
                _pool.Unspawn(obj);
                obj.transform.SetParent(_poolTransform);
            }
        }
        
        public void RecycleComponent<T>(T obj, string resPath) where T : MonoBehaviour
        {
            if (_poolModule.HasObjectPool<PoolObjectItem>(resPath))
            {
                var _pool = _poolModule.GetObjectPool<PoolObjectItem>(resPath);                _pool.Unspawn(obj);
                try
                {
                    obj.transform.SetParent(_poolTransform);
                }
                catch (Exception e)
                {
                   Log.Error(e);
                   throw;
                }
                
            }
        }

        protected override void OnRelease()
        {
            base.OnRelease();
            _released = true;
            // 只释放本管理器创建的池，不关闭资源/UI 等模块共享的整个对象池模块。
            foreach (var name in _ownedPools)
                if (_poolModule.HasObjectPool<PoolObjectItem>(name)) _poolModule.DestroyObjectPool<PoolObjectItem>(name);
            _ownedPools.Clear();
            if (_poolTransform != null) Object.Destroy(_poolTransform.gameObject);
        }
        
    }
}
