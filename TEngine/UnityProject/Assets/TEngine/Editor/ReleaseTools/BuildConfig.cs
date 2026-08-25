using TEngine.Editor;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace TEngine
{
    /// <summary>
    /// 构建模式：Release 与 Develop 互斥，打包时分别用对应的宏集合覆盖 PlayerSettings，打完包恢复原值。
    /// </summary>
    public enum EBuildMode
    {
        /// <summary>发布模式，打包时覆盖为 ReleaseDefines 宏集合。</summary>
        Release,
        /// <summary>开发模式，打包时覆盖为 DevelopDefines 宏集合（默认）。</summary>
        Develop,
    }

    public class BuildConfig
    {
        // ===== 构建模式 =====
        /// <summary>构建模式（Release/Develop 互斥），决定打包时用哪个宏集合覆盖 PlayerSettings。</summary>
        public EBuildMode BuildMode = EBuildMode.Develop;

        /// <summary>Release 模式宏集合（分号分隔），打包前覆盖到 PlayerSettings，打完包恢复原值。</summary>
        public string ReleaseDefines = "TE_RELEASE";

        /// <summary>Develop 模式宏集合（分号分隔），打包前覆盖到 PlayerSettings，打完包恢复原值。</summary>
        public string DevelopDefines = "TE_DEVELOP;ENABLE_LOG";

        // ===== 持久化键名（BuildPipelineWindow 与一键打包入口共用，避免键名分散两处） =====
        public const string PrefKey_BuildMode = "TEngine_BP_BuildMode";
        public const string PrefKey_ReleaseDefines = "TEngine_BP_ReleaseDefines";
        public const string PrefKey_DevelopDefines = "TEngine_BP_DevelopDefines";

        // 基础设置
        public BuildTarget BuildTarget;
        public EBuildPipeline BuildPipeline = EBuildPipeline.ScriptableBuildPipeline;
        public ECompressOption CompressOption = ECompressOption.LZ4;
        public EncryptionType EncryptionType = EncryptionType.None;
        public string PackageVersion = "";
        public string OutputRoot = "./Builds/";

        // 最小包设置
        public bool MinimalPackage;
        public string RetainTags = "";

        // 高级设置
        public bool EnableSharePackRule = true;
        public bool UseAssetDependencyDB = true;
        public bool ClearBuildCache;
        public bool VerifyBuildingResult = true;
        public EBuildinFileCopyOption BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll;
        public EFileNameStyle FileNameStyle = EFileNameStyle.BundleName_HashName;

        // 热更DLL设置
        public bool BuildHotFixDll = true;

        // 打包Player设置
        public bool BuildPlayer;
        public BuildTarget PlayerPlatform;
        public string PlayerOutputPath = "";

        public static BuildConfig CreateDefault()
        {
            return new BuildConfig
            {
                BuildMode = EBuildMode.Develop,
                BuildTarget = EditorUserBuildSettings.activeBuildTarget,
                PlayerPlatform = EditorUserBuildSettings.activeBuildTarget,
                PackageVersion = GetDefaultPackageVersion(),
                OutputRoot = "./Builds/",
                PlayerOutputPath = GetDefaultPlayerOutputPath(EditorUserBuildSettings.activeBuildTarget),
            };
        }

        /// <summary>
        /// 从 EditorPrefs 读取 BuildPipelineWindow 持久化的构建模式与宏集合，填入指定 config。
        /// <remarks>一键打包入口经此方法才能拿到窗口里配置好的宏，而非字段默认值。</remarks>
        /// </summary>
        public static void LoadPersistedDefines(BuildConfig config)
        {
            config.BuildMode = (EBuildMode)EditorPrefs.GetInt(PrefKey_BuildMode, (int)EBuildMode.Develop);
            config.ReleaseDefines = EditorPrefs.GetString(PrefKey_ReleaseDefines, "TE_RELEASE");
            config.DevelopDefines = EditorPrefs.GetString(PrefKey_DevelopDefines, "TE_DEVELOP;ENABLE_LOG");
        }

        /// <summary>
        /// 获取当前构建模式对应的宏集合（已解析分号/逗号、去空白）。
        /// </summary>
        public string[] GetBuildModeDefines()
        {
            return ParseDefines(BuildMode == EBuildMode.Release ? ReleaseDefines : DevelopDefines);
        }

        /// <summary>
        /// 解析分号/逗号分隔的宏字符串为数组，自动去除空白项。
        /// </summary>
        public static string[] ParseDefines(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return System.Array.Empty<string>();
            }

            var list = new System.Collections.Generic.List<string>();
            foreach (var part in raw.Split(';', '；', ','))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    list.Add(trimmed);
                }
            }
            return list.ToArray();
        }

        public static string GetDefaultPackageVersion()
        {
            int totalMinutes = System.DateTime.Now.Hour * 60 + System.DateTime.Now.Minute;
            return System.DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
        }

        public static string GetDefaultPlayerOutputPath(BuildTarget target)
        {
            string basePath = Application.dataPath + "/../Build/";
            return target switch
            {
                BuildTarget.StandaloneWindows64 => basePath + "Windows/Release_Windows.exe",
                BuildTarget.Android => basePath + $"Android/{GetDefaultPackageVersion()}Android.apk",
                BuildTarget.iOS => basePath + "IOS/XCode_Project",
                BuildTarget.StandaloneOSX => basePath + "MacOS/Release_MacOS.app",
                BuildTarget.StandaloneLinux64 => basePath + "Linux/Release_Linux",
                BuildTarget.WebGL => basePath + "WebGL",
                _ => basePath + target + "/Release"
            };
        }

        public static BuildTargetGroup GetBuildTargetGroup(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => BuildTargetGroup.Standalone,
                BuildTarget.StandaloneOSX => BuildTargetGroup.Standalone,
                BuildTarget.StandaloneLinux64 => BuildTargetGroup.Standalone,
                BuildTarget.Android => BuildTargetGroup.Android,
                BuildTarget.iOS => BuildTargetGroup.iOS,
                BuildTarget.WebGL => BuildTargetGroup.WebGL,
                BuildTarget.Switch => BuildTargetGroup.Switch,
                BuildTarget.PS4 => BuildTargetGroup.PS4,
                BuildTarget.PS5 => BuildTargetGroup.PS5,
                _ => BuildTargetGroup.Standalone
            };
        }
    }
}
