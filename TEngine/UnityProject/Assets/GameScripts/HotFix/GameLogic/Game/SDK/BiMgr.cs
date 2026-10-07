using GameSDK;
using System;
using System.Collections.Generic;
using TEngine;

namespace GameLogic
{
    /// <summary>热更业务 BI 入口，统一交由常驻 SDK 管理登录门禁与缓存。</summary>
    public static partial class BiMgr
    {
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// 参数复制后转发，null 值按空字符串处理。
        /// true 表示 SDK 本地已接收，可能等待登录或补发，不代表服务端接收成功。
        /// SDK 负责缓存和有限重试，参数无效或队列已满时返回 false。
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
                Log.Warning($"[BI] {eventId} 未接收：参数无效或 SDK 缓存已满。");
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
