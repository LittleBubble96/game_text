
using System;
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
        protected override void Release(bool isShutdown){}
    
        /// <summary>
        /// 创建Actor对象（异步加载资源，对象池冷启动时使用）。
        /// </summary>
        /// <param name="actorName">对象名称。</param>
        /// <param name="target">对象持有实例。</param>
        /// <returns></returns>
        public static async UniTask<PoolObjectItem> CreateComponentAsync<T>(string resPath) where T : MonoBehaviour
        {
            var gameObjectItem = MemoryPool.Acquire<PoolObjectItem>();
            var itemObj = await GameModule.Resource.LoadGameObjectAsync(resPath);
            var target = itemObj.GetOrAddComponent<T>();
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
            IObjectPool<PoolObjectItem> _pool = null;
            if (!_poolModule.HasObjectPool<PoolObjectItem>(resPath))
            {
                _pool = _poolModule.CreateSingleSpawnObjectPool<PoolObjectItem>(resPath, capacity);

                for (int i = 0; i < capacity; i++)
                {
                    var ret = await PoolObjectItem.CreateComponentAsync<T>(resPath);
                    _pool.Register(ret, false);
                    var obj = ret.Target as T;
                    obj.transform.SetParent(_poolTransform);
                }
            }
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
            obj.transform.SetParent(parent);
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
            _poolModule.Release();
            Object.Destroy(_poolTransform);
        }
        
    }
}
