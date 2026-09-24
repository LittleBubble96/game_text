using System;
using System.Collections.Generic;
using TEngine;

namespace GameLogic
{
    /// <summary>BI 统一入口。先调用 SDK.InitSdk；本类不主动触发业务埋点。</summary>
    public static partial class BiMgr
    {
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 参数复制后转发，null 值按空字符串处理。
        /// true 仅代表平台调用未抛异常（编辑器为日志输出），不代表服务端接收成功。
        /// 未初始化、不支持的平台或上报失败返回 false，不缓存、不重试。
        /// </summary>
        public static bool ReportEvent(string eventId, Dictionary<string, string> data = null)
        {
            if (!Enabled) return false;
            if (string.IsNullOrWhiteSpace(eventId))
            {
                Log.Warning("[BI] 事件名不能为空，已忽略上报。");
                return false;
            }

            try
            {
                var payload = new Dictionary<string, string>();
                if (data != null)
                {
                    foreach (var field in data)
                    {
                        if (string.IsNullOrWhiteSpace(field.Key))
                        {
                            Log.Warning($"[BI] {eventId} 包含空参数名，已忽略上报。");
                            return false;
                        }
                        payload.Add(field.Key, field.Value ?? string.Empty);
                    }
                }

                if (SDK.ReportEvent(eventId, payload)) return true;
                Log.Warning($"[BI] {eventId} 未上报：SDK 未初始化或当前平台不支持。");
                return false;
            }
            catch (Exception exception)
            {
                Log.Warning($"[BI] {eventId} 上报异常：{exception.Message}");
                return false;
            }
        }
    }
}
