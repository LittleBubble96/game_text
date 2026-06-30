using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 本地化组件（精简版）。
    /// 语言数据已迁移到 Luban 配置表，此组件仅保留框架模块注册和 Language 属性。
    /// Language setter 会触发全局事件 LocalizationModule.Event_LanguageChanged。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalizationManager : MonoBehaviour
    {
        private string _currentLanguage = "Chinese";

        /// <summary>
        /// 获取或设置本地化语言。
        /// 设置时若语言发生变化，会触发 GameEvent.Send(LocalizationModule.Event_LanguageChanged)。
        /// </summary>
        public Language Language
        {
            get => LocalizationUtility.GetLanguage(_currentLanguage);
            set
            {
                string newLangStr = LocalizationUtility.GetLanguageStr(value);
                if (_currentLanguage == newLangStr)
                    return;

                _currentLanguage = newLangStr;
                GameEvent.Send(LocalizationModule.Event_LanguageChanged);
            }
        }

        /// <summary>
        /// 获取系统语言。
        /// </summary>
        public Language SystemLanguage => LocalizationUtility.SystemLanguage;

        /// <summary>
        /// 框架初始化：注册 ILocalizationModule。
        /// </summary>
        private void Awake()
        {
            LocalizationModule localizationModule = new LocalizationModule();
            localizationModule.Bind(this);
            ModuleSystem.RegisterModule<ILocalizationModule>(localizationModule);
        }
    }
}
