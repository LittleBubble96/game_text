namespace TEngine
{
    /// <summary>
    /// 本地化管理模块（精简版）。
    /// 语言数据已迁移到 Luban 配置表，此模块仅做框架层 Language 属性透传。
    /// </summary>
    public class LocalizationModule : Module, ILocalizationModule
    {
        /// <summary>
        /// 语言切换全局事件 ID（由 LocalizationManager.Language setter 触发）。
        /// 监听方通过 AddUIEvent / GameEvent.AddEventListener 订阅。
        /// </summary>
        public const int Event_LanguageChanged = -2051801250; // "Event_LanguageChanged".GetHashCode()

        private LocalizationManager _localizationManager;

        /// <summary>
        /// 绑定具体的本地化管理器实现。
        /// </summary>
        public void Bind(LocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
        }

        /// <summary>模块初始化。</summary>
        public override void OnInit()
        {
        }

        /// <summary>模块关闭。</summary>
        public override void Shutdown()
        {
            UnityEngine.Object.Destroy(_localizationManager);
        }

        /// <summary>当前使用的语言。</summary>
        public Language Language
        {
            get => _localizationManager.Language;
            set => _localizationManager.Language = value;
        }

        /// <summary>系统默认语言。</summary>
        public Language SystemLanguage => _localizationManager.SystemLanguage;
    }
}
