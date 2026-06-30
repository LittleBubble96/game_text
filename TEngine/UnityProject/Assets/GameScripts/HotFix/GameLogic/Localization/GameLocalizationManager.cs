using System;
using System.Collections.Generic;
using GameConfig;
using GameConfig.language;
using GameLogic.Data;
using TEngine;
using UnityEngine;

namespace GameLogic.Localization
{
    /// <summary>
    /// 游戏层多语言管理器。
    /// 从 Luban 配置表（TbLanguage / TbLanguageContent）读取翻译文本，
    /// 语言选择持久化到 GameSettingsData.language（GameCacheManager）和 PlayerPrefs（双写保持一致）。
    /// </summary>
    public class GameLocalizationManager : Singleton<GameLocalizationManager>
    {
        #region 语言数据

        /// <summary>当前语言代码（如 "Ch"、"En"，对应 GameSettingsData.language）</summary>
        public string CurrentLanguageCode { get; private set; } = "Ch";

        /// <summary>当前语言在 Value 数组中的索引</summary>
        public int CurrentLanguageIndex { get; private set; } = 0;

        /// <summary>所有可用语言配置列表</summary>
        public List<ConfLanguage> AvailableLanguages { get; private set; } = new List<ConfLanguage>();

        /// <summary>
        /// LanguageCode（游戏枚举）到 TEngine Language 枚举的映射，
        /// 用于同步写入 PlayerPrefs（ProcedureLaunch.InitLanguageSettings 使用）。
        /// </summary>
        private static readonly Dictionary<LanguageCode, Language> LanguageCodeToTEngineMap = new Dictionary<LanguageCode, Language>
        {
            { LanguageCode.Ch, Language.ChineseSimplified },
            { LanguageCode.En, Language.English },
        };

        #endregion

        #region 初始化

        protected override void OnInit()
        {
            base.OnInit();
            LoadLanguageConfigs();
            ApplySavedLanguage();
        }

        /// <summary>从 Luban 表加载语言配置</summary>
        private void LoadLanguageConfigs()
        {
            AvailableLanguages.Clear();

            var langTable = ConfigSystem.Instance.Tables.TbLanguage;
            if (langTable == null || langTable.DataMap.Count == 0)
            {
                Debug.LogError("[GameLocalization] TbLanguage 表为空，多语言功能不可用。");
                return;
            }

            foreach (var kv in langTable.DataMap)
            {
                AvailableLanguages.Add(kv.Value);
            }

            // 按 Id 排序，保证稳定顺序
            AvailableLanguages.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        /// <summary>从缓存中恢复用户选择的语言（优先 GameSettingsData，兜底 PlayerPrefs）</summary>
        private void ApplySavedLanguage()
        {
            // 优先从 GameCacheManager（新系统）恢复
            var cacheData = GameManager.Instance?.CacheManager?.CacheData?.gameSettingsData;
            if (cacheData != null && !string.IsNullOrEmpty(cacheData.language))
            {
                SetLanguageByCode(cacheData.language, saveToCache: false);
                return;
            }

            // 兜底：从 PlayerPrefs（旧系统/ProcedureLaunch 写入）恢复，迁移到新系统
            if (Utility.PlayerPrefs.HasSetting(Constant.Setting.Language))
            {
                try
                {
                    string languageString = Utility.PlayerPrefs.GetString(Constant.Setting.Language);
                    var tengineLang = (Language)Enum.Parse(typeof(Language), languageString);
                    var code = MapTEngineToLanguageCode(tengineLang);
                    if (code.HasValue)
                    {
                        SetLanguage(code.Value, saveToCache: true); // 迁移：写入 GameSettingsData
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameLocalization] 从 PlayerPrefs 恢复语言失败: {e.Message}");
                }
            }
        }

        #endregion

        #region 翻译查询

        /// <summary>
        /// 根据多语言 Key 获取对应当前语言的文本。
        /// 使用示例：GameLocalizationManager.Instance.GetText(LanguageKey.submit_btn)
        /// </summary>
        /// <param name="key">多语言 Key，从 LanguageKey 常量类获取</param>
        /// <returns>翻译后的文本，找不到则返回 key 本身</returns>
        public string GetText(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var contentTable = ConfigSystem.Instance.Tables.TbLanguageContent;
            if (contentTable == null)
            {
                Debug.LogWarning($"[GameLocalization] TbLanguageContent 表不可用。");
                return key;
            }

            var entry = contentTable.GetOrDefault(key);
            if (entry == null || entry.Value == null)
            {
                Debug.LogWarning($"[GameLocalization] 未找到多语言 Key: {key}");
                return key;
            }

            if (CurrentLanguageIndex < 0 || CurrentLanguageIndex >= entry.Value.Length)
            {
                Debug.LogWarning($"[GameLocalization] 语言索引 {CurrentLanguageIndex} 超出 {key} 的值数组范围。");
                return key;
            }

            return entry.Value[CurrentLanguageIndex] ?? key;
        }

        #endregion

        #region 语言切换

        /// <summary>
        /// 通过语言代码（如 "Ch"、"En"）设置当前语言。
        /// </summary>
        /// <param name="languageCode">语言代码</param>
        /// <param name="saveToCache">是否同时持久化到缓存</param>
        /// <returns>是否设置成功</returns>
        public bool SetLanguageByCode(string languageCode, bool saveToCache = true)
        {
            if (string.IsNullOrEmpty(languageCode))
                return false;

            // 尝试解析 LanguageCode 枚举
            if (!Enum.TryParse<LanguageCode>(languageCode, true, out var langEnum))
            {
                Debug.LogWarning($"[GameLocalization] 未知语言代码: {languageCode}");
                return false;
            }

            return SetLanguage(langEnum, saveToCache);
        }

        /// <summary>
        /// 通过 LanguageCode 枚举设置当前语言。
        /// </summary>
        public bool SetLanguage(LanguageCode code, bool saveToCache = true)
        {
            int index = (int)code - 1;
            if (index < 0)
            {
                Debug.LogWarning($"[GameLocalization] 无效的 LanguageCode: {code}");
                return false;
            }

            CurrentLanguageCode = code.ToString();
            CurrentLanguageIndex = index;

            if (saveToCache)
            {
                SaveLanguageToCache();
            }

            Debug.Log($"[GameLocalization] 语言切换为: {CurrentLanguageCode} (索引={CurrentLanguageIndex})");

            // 同步框架层语言并触发全局事件（LocalizationManager.Language setter 内部发送 Event_LanguageChanged）
            if (LanguageCodeToTEngineMap.TryGetValue(code, out var tengineLang))
            {
                ModuleSystem.GetModule<ILocalizationModule>().Language = tengineLang;
            }

            return true;
        }

        /// <summary>
        /// 通过语言列表索引设置当前语言。
        /// </summary>
        public bool SetLanguageByIndex(int index, bool saveToCache = true)
        {
            if (index < 0 || index >= AvailableLanguages.Count)
            {
                Debug.LogWarning($"[GameLocalization] 无效的语言索引: {index}");
                return false;
            }

            return SetLanguage(AvailableLanguages[index].LanguageCode, saveToCache);
        }

        /// <summary>将当前语言设置保存到缓存（双写：GameSettingsData + PlayerPrefs）</summary>
        private void SaveLanguageToCache()
        {
            // 写入 GameCacheManager（新系统，JSON）
            var cacheData = GameManager.Instance?.CacheManager?.CacheData?.gameSettingsData;
            if (cacheData != null)
            {
                cacheData.language = CurrentLanguageCode;
                GameManager.Instance?.CacheManager?.Save();
            }

            // 同步写入 PlayerPrefs（旧系统/ProcedureLaunch.InitLanguageSettings 使用）
            if (Enum.TryParse<LanguageCode>(CurrentLanguageCode, true, out var code)
                && LanguageCodeToTEngineMap.TryGetValue(code, out var tengineLang))
            {
                Utility.PlayerPrefs.SetString(Constant.Setting.Language, tengineLang.ToString());
                Utility.PlayerPrefs.Save();
            }
        }

        #endregion

        #region 枚举映射

        /// <summary>TEngine Language → LanguageCode 反向映射</summary>
        private static LanguageCode? MapTEngineToLanguageCode(Language tengineLang)
        {
            foreach (var kv in LanguageCodeToTEngineMap)
            {
                if (kv.Value == tengineLang)
                    return kv.Key;
            }
            return null;
        }

        #endregion

        #region 语言配置查询

        /// <summary>获取当前语言的字体文件名</summary>
        public string GetCurrentLanguageFont()
        {
            var lang = GetCurrentLanguageConfig();
            return lang?.Font ?? string.Empty;
        }

        /// <summary>获取当前语言的字体纵向偏移</summary>
        public float GetCurrentLanguageBaseLine()
        {
            var lang = GetCurrentLanguageConfig();
            return lang?.BaseLine ?? 0f;
        }

        /// <summary>通过 LanguageCode 获取语言配置</summary>
        public ConfLanguage GetLanguageConfig(LanguageCode code)
        {
            return AvailableLanguages.Find(l => l.LanguageCode == code);
        }

        /// <summary>获取当前语言配置</summary>
        public ConfLanguage GetCurrentLanguageConfig()
        {
            if (!Enum.TryParse<LanguageCode>(CurrentLanguageCode, true, out var code))
                return null;
            return GetLanguageConfig(code);
        }

        #endregion
    }
}
