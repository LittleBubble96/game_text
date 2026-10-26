using System;
using Newtonsoft.Json;

namespace GameSDK
{
    public enum LevelReportResult { Success = 1, Enter = 5, Exit = 6, UseProp = 7 }

    /// <summary>微信关卡事件的不可变快照；仅包含已启用的字段与动作。</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class LevelReport
    {
        public const string EventId = "10000003";
        [JsonProperty("eventID")] public string EventID => EventId;
        [JsonProperty("levelID")] public int LevelId { get; }
        [JsonProperty("levelName")] public string LevelName { get; }
        [JsonProperty("levelAction")] public int LevelAction => 1;
        [JsonProperty("levelResult")] public int Result { get; }
        [JsonProperty("levelProgress")] public int Progress { get; }
        [JsonProperty("gameVersion")] public int GameVersion { get; }
        [JsonProperty("levelTime", NullValueHandling = NullValueHandling.Ignore)] public int? LevelTime { get; }
        [JsonProperty("loginCount", NullValueHandling = NullValueHandling.Ignore)] public int? LoginCount { get; }

        public LevelReport(int levelId, string levelName, LevelReportResult result, int progress,
            int gameVersion, int levelTime = 0, int loginCount = 1)
        {
            if (levelId <= 0) throw new ArgumentOutOfRangeException(nameof(levelId));
            if (string.IsNullOrWhiteSpace(levelName)) throw new ArgumentException("关卡名称不能为空", nameof(levelName));
            if (result != LevelReportResult.Success && result != LevelReportResult.Enter &&
                result != LevelReportResult.Exit && result != LevelReportResult.UseProp)
                throw new ArgumentOutOfRangeException(nameof(result));
            LevelId = levelId;
            LevelName = levelName;
            Result = (int)result;
            Progress = Math.Max(0, Math.Min(100, progress));
            GameVersion = Math.Max(0, gameVersion);
            if (result == LevelReportResult.Success) LevelTime = Math.Max(0, levelTime);
            if (result == LevelReportResult.Enter) LoginCount = Math.Max(1, loginCount);
        }

        public string ToJson() => JsonConvert.SerializeObject(this);

        /// <summary>major * 10000 + minor * 100 + patch；未知版本为0。</summary>
        public static int EncodeVersion(string value)
        {
            if (!Version.TryParse(value, out var version) || version.Minor > 99 || version.Build > 99) return 0;
            long encoded = (long)version.Major * 10000 + version.Minor * 100 + Math.Max(0, version.Build);
            return encoded <= int.MaxValue ? (int)encoded : 0;
        }
    }
}
