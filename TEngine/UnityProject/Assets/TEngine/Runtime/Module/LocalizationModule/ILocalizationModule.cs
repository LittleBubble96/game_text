namespace TEngine
{
    /// <summary>
    /// 本地化模块接口（精简版）。
    /// 语言数据已迁移到 Luban 配置表，此接口仅保留 Language 属性。
    /// </summary>
    public interface ILocalizationModule
    {
        /// <summary>
        /// 获取或设置本地化语言。
        /// </summary>
        public Language Language { get; set; }

        /// <summary>
        /// 获取系统语言。
        /// </summary>
        public Language SystemLanguage { get; }
    }
}
