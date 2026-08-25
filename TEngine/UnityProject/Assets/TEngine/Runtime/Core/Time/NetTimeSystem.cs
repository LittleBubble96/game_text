using System;
// using Log;
// using Logic.Message;
using UnityEngine;
using WeChatWASM;

namespace TEngine
{
    public partial class NetTimeSystem
    {
        private const float RequestInterval = 60f;
        private const int RequestTimeOut = 3000;
        private const float TimeMaxOffset = 300f;

        private DateTime _netDateTime = DateTime.MinValue;

        private float _requestTime = 0;
        private float _realtime = float.MinValue;

        private Action<DateTime> _onSetNetTimeCallback;
        private float _secondTime = 2;
        private DateTime MinTime = new DateTime(2023, 1, 1);

        /// <summary>
        /// 调试加时
        /// </summary>
        private TimeSpan _addTime;

        /// <summary>
        /// 是否通过国内网络调时
        /// </summary>
        private bool _isHomeNet;

        // public static NetTimeSystem Create()
        // {
        //     var mgr = new NetTimeSystem();
        //     return mgr;
        // }
        
        protected override void OnInit()
        {
            base.OnInit();
            RefreshNetTime();
        }

        private void Update()
        {
            _requestTime += Time.deltaTime;
            if (_requestTime >= RequestInterval)
            {
                RefreshNetTime();
            }

            if (!NetTimeIsCorrect())
            {
                return;
            }

            _secondTime += Time.deltaTime;

            if (_secondTime > 1)
            {
                _secondTime -= 1;
            }
        }

        /// <summary>
        /// 获取本地时间
        /// </summary>
        public DateTime GetNow()
        {
#if UNITY_EDITOR
            return DateTime.Now;
#endif
            return DateTime.Now;
        }

        /// <summary>
        /// 时间是否校准
        /// </summary>
        public bool NetTimeIsCorrect()
        {
            //return (Time.realtimeSinceStartup - _realtime) < (RequestInterval + TimeMaxOffset);
#if DEV_BUILD
            var isNetTime = PlayerPrefs.GetInt("SetIsNetTime", 1);
            if (isNetTime == 0)
            {
                return true;
            }
#endif
            return _netDateTime != DateTime.MinValue;
        }

        /// <summary>
        ///  1.先获取SDK时间，拿不到再获取服务器时间，再获取不到，则获取本地时间
        /// 时间校准过获取校准后的时间,否则获取本地时间
        /// </summary>
        public DateTime GetNetTime()
        {
#if DEV_BUILD
            var isNetTime = PlayerPrefs.GetInt("SetIsNetTime", 1);
            if (isNetTime == 0)
            {
                return GetNow();
            }
#endif
            var result = !NetTimeIsCorrect() ? DateTime.Now : _netDateTime.AddSeconds(Time.realtimeSinceStartup - _realtime);
            result += _addTime;
            return result;
        }

        public void RefreshNetTime()
        {
            _requestTime = 0;
#if UNITY_EDITOR
            GenerateNetTimeByHttps();
            GenerateNetTimeByNtp();
#elif UNITY_WEBGL
            GenerateNetTimeByWebgl();
#endif
            
            
        }

        public void GetNetTimeAsync(Action<DateTime> action)
        {
            if (NetTimeIsCorrect())
            {
                action?.Invoke(GetNetTime());
            }
            else
            {
                GetNextNetTime(action);
            }
        }

        public void GetNextNetTime(Action<DateTime> action)
        {
            _onSetNetTimeCallback += action;
        }

        public void AddNetTime(TimeSpan ts)
        {
            _addTime += ts;
        }

        public void ResetAddNetTime()
        {
            _addTime = default;
        }

        private void SetNetTime(string url, DateTime time)
        {
            if (time < MinTime)
            {
                return;
            }

            _realtime = Time.realtimeSinceStartup;
            _netDateTime = time;

            _onSetNetTimeCallback?.Invoke(_netDateTime);
            _onSetNetTimeCallback = null;
        }

        private void SetServerTime(DateTime dateTime)
        {
            if (!NetTimeIsCorrect())
            {
                SetNetTime("server", dateTime.ToLocalTime());
            }
        }

        public void SetHomeNet(bool isHomeNet)
        {
            _isHomeNet = isHomeNet;
        }
    }
}