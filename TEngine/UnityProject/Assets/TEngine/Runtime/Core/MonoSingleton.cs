using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// MonoBehaviour 泛型单例基类。
    /// 继承此类的组件在整个场景中只存在一个实例，可选 DontDestroyOnLoad。
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();

        /// <summary>是否在切换场景时保留实例</summary>
        protected virtual bool DontDestroy => false;

        /// <summary>单例实例</summary>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = FindObjectOfType<T>();

                            if (_instance == null)
                            {
                                var go = new GameObject(typeof(T).Name);
                                _instance = go.AddComponent<T>();
                            }

                            if (_instance.DontDestroy)
                            {
                                DontDestroyOnLoad(_instance.gameObject);
                            }
                        }
                    }
                }

                return _instance;
            }
        }

        /// <summary>实例是否有效</summary>
        public static bool IsValid => _instance != null;

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (DontDestroy)
                {
                    DontDestroyOnLoad(gameObject);
                }
                OnInit();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>初始化回调（Awake 中调用，仅首次实例）</summary>
        protected virtual void OnInit() { }
        
        public virtual void Activate()
        {
            
        }
    }
}