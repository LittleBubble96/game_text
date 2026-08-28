using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TEngine.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using WeChatWASM;
using YooAsset;
using YooAsset.Editor;
using BuildResult = UnityEditor.Build.Reporting.BuildResult;

namespace TEngine
{
    /// <summary>
    /// 打包工具类。
    /// <remarks>通过CommandLineReader可以不前台开启Unity实现静默打包以及CLI工作流，详见CommandLineReader.cs example1</remarks>
    /// </summary>
    public static class ReleaseTools
    {
        #region CLI 入口

        public static void BuildDll()
        {
            string platform = CommandLineReader.GetCustomArgument("platform");
            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError($"Build Asset Bundle Error！platform is null");
                return;
            }

            BuildTarget target = GetBuildTarget(platform);

            // BuildDLLCommand.BuildAndCopyDlls(target);
        }

        public static void BuildAssetBundle()
        {
            string outputRoot = CommandLineReader.GetCustomArgument("outputRoot");
            if (string.IsNullOrEmpty(outputRoot))
            {
                Debug.LogError($"Build Asset Bundle Error！outputRoot is null");
                return;
            }

            string packageVersion = CommandLineReader.GetCustomArgument("packageVersion");
            if (string.IsNullOrEmpty(packageVersion))
            {
                Debug.LogError($"Build Asset Bundle Error！packageVersion is null");
                return;
            }

            string platform = CommandLineReader.GetCustomArgument("platform");
            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError($"Build Asset Bundle Error！platform is null");
                return;
            }

            BuildTarget target = GetBuildTarget(platform);
            BuildInternal(target, outputRoot);
            Debug.LogWarning($"Start BuildPackage BuildTarget:{target} outputPath:{outputRoot}");
        }

        #endregion

        #region MenuItem 入口（兼容原有菜单）

        [MenuItem("TEngine/Build/一键打包AssetBundle _F8")]
        public static void BuildCurrentPlatformAB()
        {
            var config = BuildConfig.CreateDefault();
            BuildConfig.LoadPersistedDefines(config);
            config.BuildHotFixDll = true;
            BuildWithConfig(config, buildPlayer: false);

            // 将 StreamingAssets 拷贝到指定目录（清空原目录后拷贝，不拷贝 .meta）
            CopyStreamingAssetsToDirectory("../../output/webgl/StreamingAssets/package");
        }
        
        [MenuItem("TEngine/Build/一键打包Webgl(Release)", false, 30)]
        public static void AutomationBuildWebglRelease()
        {
            AutomationBuildWebglInternal(EBuildMode.Release);
        }

        [MenuItem("TEngine/Build/一键打包Webgl(Develop)", false, 31)]
        public static void AutomationBuildWebglDevelop()
        {
            AutomationBuildWebglInternal(EBuildMode.Develop);
        }

        /// <summary>
        /// WebGL 一键打包内部实现：按指定构建模式设置 TE_RELEASE / TE_DEVELOP 宏后构建 AB，再转换为微信小游戏。
        /// <remarks>DoExport 作为构建后回调执行，确保微信转换期间模式宏仍生效；回调结束后由 BuildWithConfig 恢复原宏。</remarks>
        /// </summary>
        private static void AutomationBuildWebglInternal(EBuildMode mode)
        {
            var config = BuildConfig.CreateDefault();
            // 读窗口里持久化配置的宏集合，而非字段默认值；BuildMode 以菜单项指定为准
            BuildConfig.LoadPersistedDefines(config);
            config.BuildTarget = BuildTarget.WebGL;
            config.OutputRoot = Application.dataPath + "/../Builds/WebGL";
            config.BuildPlayer = false;
            config.BuildMode = mode;
            BuildWithConfig(config, buildPlayer: false, postBuildCallback: () =>
            {
                if (WXConvertCore.DoExport() == WXConvertCore.WXExportError.SUCCEED)
                {
                    Debug.Log("[Build] WebGL 转换为微信小游戏成功");
                }
                else
                {
                    Debug.LogError("[Build] WebGL 转换为微信小游戏失败");
                }
            });
        }

        [MenuItem("TEngine/Build/一键打包Window", false, 30)]
        public static void AutomationBuild()
        {
            var config = BuildConfig.CreateDefault();
            BuildConfig.LoadPersistedDefines(config);
            config.BuildTarget = BuildTarget.StandaloneWindows64;
            config.OutputRoot = Application.dataPath + "/../Builds/Windows";
            config.BuildPlayer = true;
            config.PlayerPlatform = BuildTarget.StandaloneWindows64;
            config.PlayerOutputPath = $"{Application.dataPath}/../Build/Windows/Release_Windows.exe";
            BuildWithConfig(config, buildPlayer: true);
        }

        [MenuItem("TEngine/Build/一键打包Android", false, 30)]
        public static void AutomationBuildAndroid()
        {
            var config = BuildConfig.CreateDefault();
            BuildConfig.LoadPersistedDefines(config);
            config.BuildTarget = BuildTarget.Android;
            config.OutputRoot = Application.dataPath + "/../Bundles";
            config.BuildPlayer = true;
            config.PlayerPlatform = BuildTarget.Android;
            config.PlayerOutputPath = $"{Application.dataPath}/../Build/Android/{BuildConfig.GetDefaultPackageVersion()}Android.apk";
            BuildWithConfig(config, buildPlayer: true);
        }

        [MenuItem("TEngine/Build/一键打包IOS", false, 30)]
        public static void AutomationBuildIOS()
        {
            var config = BuildConfig.CreateDefault();
            BuildConfig.LoadPersistedDefines(config);
            config.BuildTarget = BuildTarget.iOS;
            config.OutputRoot = Application.dataPath + "/../Bundles";
            config.BuildPlayer = true;
            config.PlayerPlatform = BuildTarget.iOS;
            config.PlayerOutputPath = $"{Application.dataPath}/../Build/IOS/XCode_Project";
            BuildWithConfig(config, buildPlayer: true);
        }

        #endregion

        #region 参数化构建入口

        /// <summary>
        /// 通过 BuildConfig 执行完整构建流程。
        /// <remarks>打包前用 BuildMode 对应的宏集合覆盖目标平台宏 -> 打包 -> finally 恢复。
        /// 仅覆盖当前打包目标平台（不影响其他平台），用 SetDefines 直接写入并刷新。</remarks>
        /// </summary>
        /// <param name="postBuildCallback">构建完成后、恢复宏之前执行的回调。
        /// 用于必须在“模式宏仍生效”期间执行的后续步骤（如微信小游戏 WXConvertCore.DoExport）。</param>
        public static void BuildWithConfig(BuildConfig config, bool buildPlayer, Action postBuildCallback = null)
        {
            var modeDefines = config.GetBuildModeDefines();
            Debug.Log($"[BuildWithConfig] 构建模式: {config.BuildMode} (宏: {string.Join(";", modeDefines)})");

            // 0. 仅对目标平台：备份原宏 -> 覆盖为模式宏 -> 打包 -> 回调 -> finally 恢复
            BuildTarget target = (buildPlayer || config.BuildPlayer) ? config.PlayerPlatform : config.BuildTarget;
            BuildTargetGroup targetGroup = BuildConfig.GetBuildTargetGroup(target);

            string[] backup = null;
            try
            {
                backup = ScriptingDefineSymbols.GetScriptingDefineSymbols(targetGroup);
                ScriptingDefineSymbols.SetDefines(targetGroup, modeDefines);

                // 1. [可选] 编译热更DLL
                if (config.BuildHotFixDll)
                {
                    Debug.Log("[BuildWithConfig] 编译热更DLL...");
                    BuildDLLCommand.BuildAndCopyDlls();
                }

                // 2. 刷新资源
                AssetDatabase.Refresh();

                // 3. 构建 AssetBundle
                var buildResult = BuildInternalWithConfig(config);
                if (!buildResult.Success)
                {
                    Debug.LogError($"[BuildWithConfig] AssetBundle构建失败: {buildResult.ErrorInfo}");
                    return;
                }

                Debug.Log($"[BuildWithConfig] AssetBundle构建成功: {buildResult.OutputPackageDirectory}");

                // 4. [最小包] 删除 StreamingAssets 中的 .bundle 文件
                if (config.MinimalPackage)
                {
                    ProcessMinimalPackage(config.PackageVersion, config.RetainTags, buildResult.OutputPackageDirectory);
                }

                // 5. 刷新资源
                AssetDatabase.Refresh();

                // 6. [可选] 构建 Player
                if (buildPlayer || config.BuildPlayer)
                {
                    BuildImp(
                        BuildConfig.GetBuildTargetGroup(config.PlayerPlatform),
                        config.PlayerPlatform,
                        config.PlayerOutputPath
                    );
                }

                // 7. 构建后回调（此时模式宏仍生效；回调结束后 finally 才恢复原宏）
                postBuildCallback?.Invoke();
            }
            finally
            {
                // 8. 恢复打包前的原始宏
                if (backup != null)
                {
                    ScriptingDefineSymbols.SetDefines(targetGroup, backup);
                    Debug.Log("[BuildWithConfig] 已恢复打包前的原始宏定义");
                }
            }
        }

        #endregion

        #region AssetBundle 构建

        private static YooAsset.Editor.BuildResult BuildInternalWithConfig(BuildConfig config)
        {
            Debug.Log($"开始构建 : {config.BuildTarget}");

            IBuildPipeline pipeline;
            BuildParameters buildParameters;

            if (config.BuildPipeline == EBuildPipeline.BuiltinBuildPipeline)
            {
                var builtinBuildParameters = new BuiltinBuildParameters();
                pipeline = new BuiltinBuildPipeline();
                buildParameters = builtinBuildParameters;
                builtinBuildParameters.CompressOption = config.CompressOption;
            }
            else
            {
                var scriptableBuildParameters = new ScriptableBuildParameters();
                pipeline = new ScriptableBuildPipeline();
                buildParameters = scriptableBuildParameters;
                scriptableBuildParameters.CompressOption = config.CompressOption;
                scriptableBuildParameters.BuiltinShadersBundleName = GetBuiltinShaderBundleName("DefaultPackage");
                scriptableBuildParameters.ReplaceAssetPathWithAddress = Settings.UpdateSetting.GetReplaceAssetPathWithAddress();
            }

            string outputRoot = config.OutputRoot;
            if (!Path.IsPathRooted(outputRoot))
            {
                outputRoot = Path.Combine(Application.dataPath + "/../", outputRoot);
                outputRoot = Path.GetFullPath(outputRoot).Replace('\\', '/');
            }

            buildParameters.BuildOutputRoot = outputRoot;
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = config.BuildPipeline.ToString();
            buildParameters.BuildTarget = config.BuildTarget;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.PackageName = "DefaultPackage";
            buildParameters.PackageVersion = config.PackageVersion;
            buildParameters.VerifyBuildingResult = config.VerifyBuildingResult;
            buildParameters.EnableSharePackRule = config.EnableSharePackRule;
            buildParameters.FileNameStyle = config.FileNameStyle;
            buildParameters.BuildinFileCopyOption = config.BuildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = string.Empty;
            buildParameters.EncryptionServices = GetEncryptionFromType(config.EncryptionType);
            buildParameters.ClearBuildCacheFiles = config.ClearBuildCache;
            buildParameters.UseAssetDependencyDB = config.UseAssetDependencyDB;

            var result = pipeline.Run(buildParameters, true);
            return result;
        }

        /// <summary>
        /// 旧版 BuildInternal，供 CLI 入口兼容
        /// </summary>
        private static void BuildInternal(BuildTarget buildTarget, string outputRoot, string packageVersion = "1.0",
            EBuildPipeline buildPipeline = EBuildPipeline.ScriptableBuildPipeline)
        {
            Debug.Log($"开始构建 : {buildTarget}");

            IBuildPipeline pipeline = null;
            BuildParameters buildParameters = null;

            if (buildPipeline == EBuildPipeline.BuiltinBuildPipeline)
            {
                BuiltinBuildParameters builtinBuildParameters = new BuiltinBuildParameters();
                pipeline = new BuiltinBuildPipeline();
                buildParameters = builtinBuildParameters;
                builtinBuildParameters.CompressOption = ECompressOption.LZ4;
            }
            else
            {
                ScriptableBuildParameters scriptableBuildParameters = new ScriptableBuildParameters();
                pipeline = new ScriptableBuildPipeline();
                buildParameters = scriptableBuildParameters;
                scriptableBuildParameters.CompressOption = ECompressOption.LZ4;
                scriptableBuildParameters.BuiltinShadersBundleName = GetBuiltinShaderBundleName("DefaultPackage");
                scriptableBuildParameters.ReplaceAssetPathWithAddress = Settings.UpdateSetting.GetReplaceAssetPathWithAddress();
            }

            buildParameters.BuildOutputRoot = outputRoot;
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = buildPipeline.ToString();
            buildParameters.BuildTarget = buildTarget;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.PackageName = "DefaultPackage";
            buildParameters.PackageVersion = packageVersion;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.EnableSharePackRule = true;
            buildParameters.FileNameStyle = EFileNameStyle.BundleName_HashName;
            buildParameters.BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll;
            buildParameters.BuildinFileCopyParams = string.Empty;
            buildParameters.EncryptionServices = GetEncryptionFromResourceModuleDriver();
            buildParameters.ClearBuildCacheFiles = false;
            buildParameters.UseAssetDependencyDB = true;

            var buildResult = pipeline.Run(buildParameters, true);
            if (buildResult.Success)
            {
                Debug.Log($"构建成功 : {buildResult.OutputPackageDirectory}");
            }
            else
            {
                Debug.LogError($"构建失败 : {buildResult.ErrorInfo}");
            }
        }

        #endregion

        #region 最小包后处理
        /// <summary>
        /// 读取文件的文本数据
        /// </summary>
        public static string ReadAllText(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                return null;
            }
            return File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// 最小包模式：删除 StreamingAssets 中不带保留 tag 的 .bundle 文件
        /// 使用构建输出的 BuildReport（JSON）获取 bundle 的 tag 信息
        /// </summary>
        public static void ProcessMinimalPackage(string packageVersion, string retainTags, string outputPackageDirectory)
        {
            string streamingRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            string packageName = "DefaultPackage";

            // 定位构建报告文件
            string reportFileName = YooAssetSettingsData.GetBuildReportFileName(packageName, packageVersion);
            string reportPath = $"{outputPackageDirectory}/{reportFileName}";

            if (!File.Exists(reportPath))
            {
                Debug.LogError($"[最小包] 未找到构建报告: {reportPath}，跳过最小包处理");
                return;
            }

            // 反序列化 BuildReport
            YooAsset.Editor.BuildReport buildReport;
            try
            {
                string jsonData = ReadAllText(reportPath);
                buildReport = YooAsset.Editor.BuildReport.Deserialize(jsonData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[最小包] 反序列化构建报告失败: {e.Message}");
                return;
            }

            // 构建保留文件名集合
            HashSet<string> retainFileNames = new HashSet<string>();
            string[] retainTagArray = ParseRetainTags(retainTags);

            if (retainTagArray.Length > 0)
            {
                foreach (var bundleInfo in buildReport.BundleInfos)
                {
                    if (bundleInfo.Tags != null && HasTag(bundleInfo.Tags, retainTagArray))
                    {
                        retainFileNames.Add(bundleInfo.FileName);
                    }
                }
                Debug.Log($"[最小包] 保留 Tag: [{string.Join(", ", retainTagArray)}]，匹配 {retainFileNames.Count} 个 bundle");
            }

            // 扫描 StreamingAssets 下的 .bundle 文件
            if (!Directory.Exists(streamingRoot))
            {
                Debug.LogWarning($"[最小包] StreamingAssets 目录不存在: {streamingRoot}");
                return;
            }

            string[] bundleFiles = Directory.GetFiles(streamingRoot, "*.bundle", SearchOption.AllDirectories);
            int deletedCount = 0;
            int retainedCount = 0;

            foreach (var file in bundleFiles)
            {
                string fileName = Path.GetFileName(file);
                if (retainFileNames.Contains(fileName))
                {
                    retainedCount++;
                    Debug.Log($"[最小包] 保留: {fileName}");
                }
                else
                {
                    File.Delete(file);
                    deletedCount++;
                    Debug.Log($"[最小包] 删除: {fileName}");
                }
            }

            Debug.Log($"[最小包] 处理完成 - 删除 {deletedCount} 个 .bundle，保留 {retainedCount} 个 .bundle");

            // 删除空目录
            CleanEmptyDirectories(streamingRoot);
        }

        private static bool HasTag(string[] bundleTags, string[] matchTags)
        {
            foreach (var matchTag in matchTags)
            {
                foreach (var bundleTag in bundleTags)
                {
                    if (bundleTag == matchTag)
                        return true;
                }
            }
            return false;
        }

        private static string[] ParseRetainTags(string retainTags)
        {
            if (string.IsNullOrWhiteSpace(retainTags))
                return Array.Empty<string>();

            return retainTags
                .Split(',', '，') // 支持中英文逗号
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
        }

        private static void CleanEmptyDirectories(string rootPath)
        {
            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                CleanEmptyDirectories(dir);
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
        }

        #endregion

        #region StreamingAssets 拷贝

        /// <summary>
        /// 将 StreamingAssets 目录下的所有文件递归拷贝到指定目录，不拷贝 .meta 文件。
        /// </summary>
        private static void CopyStreamingAssetsToDirectory(string targetDir)
        {
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                Debug.LogError("[StreamingAssets拷贝] 目标目录为空，跳过拷贝");
                return;
            }

            string sourceRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError($"[StreamingAssets拷贝] 源目录不存在: {sourceRoot}，跳过拷贝");
                return;
            }

            string targetRoot = targetDir;
            if (!Path.IsPathRooted(targetRoot))
            {
                targetRoot = Path.Combine(Application.dataPath + "/../", targetRoot);
                targetRoot = Path.GetFullPath(targetRoot).Replace('\\', '/');
            }

            // 清空原目录后再拷贝
            try
            {
                if (Directory.Exists(targetRoot))
                {
                    Directory.Delete(targetRoot, true);
                    Debug.Log($"[StreamingAssets拷贝] 已清空目标目录: {targetRoot}");
                }
                Directory.CreateDirectory(targetRoot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamingAssets拷贝] 目标目录清空失败: {targetRoot}，{e.Message}");
                return;
            }

            int copiedCount = 0;
            int skippedMetaCount = 0;

            // 拷贝所有文件（递归），跳过 .meta
            string[] files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
            foreach (string sourceFile in files)
            {
                string fileName = Path.GetFileName(sourceFile);
                if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    skippedMetaCount++;
                    continue;
                }

                string relative = sourceFile.Substring(sourceRoot.Length)
                    .TrimStart('/', '\\')
                    .Replace('\\', '/');
                string targetFile = Path.Combine(targetRoot, relative);

                string targetFileDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetFileDir) && !Directory.Exists(targetFileDir))
                {
                    Directory.CreateDirectory(targetFileDir);
                }

                File.Copy(sourceFile, targetFile, true);
                copiedCount++;
            }

            Debug.Log($"[StreamingAssets拷贝] 完成: {sourceRoot} -> {targetRoot}（拷贝 {copiedCount} 个文件，跳过 {skippedMetaCount} 个 .meta）");
        }

        #endregion

        #region Player 构建

        public static void BuildImp(BuildTargetGroup buildTargetGroup, BuildTarget buildTarget, string locationPathName)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
            AssetDatabase.Refresh();

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray(),
                locationPathName = locationPathName,
                targetGroup = buildTargetGroup,
                target = buildTarget,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build success: {summary.totalSize / 1024 / 1024} MB, {summary.outputPath}");
            }
            else
            {
                Debug.Log($"Build Failed" + summary.result);
            }
        }

        #endregion

        #region 工具方法

        private static BuildTarget GetBuildTarget(string platform)
        {
            BuildTarget target = BuildTarget.NoTarget;
            switch (platform)
            {
                case "Android":
                    target = BuildTarget.Android;
                    break;
                case "IOS":
                    target = BuildTarget.iOS;
                    break;
                case "Windows":
                    target = BuildTarget.StandaloneWindows64;
                    break;
                case "MacOS":
                    target = BuildTarget.StandaloneOSX;
                    break;
                case "Linux":
                    target = BuildTarget.StandaloneLinux64;
                    break;
                case "WebGL":
                    target = BuildTarget.WebGL;
                    break;
                case "Switch":
                    target = BuildTarget.Switch;
                    break;
                case "PS4":
                    target = BuildTarget.PS4;
                    break;
                case "PS5":
                    target = BuildTarget.PS5;
                    break;
            }

            return target;
        }

        private static string GetBuiltinShaderBundleName(string packageName)
        {
            var uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
            var packRuleResult = DefaultPackRule.CreateShadersPackRuleResult();
            return packRuleResult.GetBundleName(packageName, uniqueBundleName);
        }

        /// <summary>
        /// 根据 EncryptionType 枚举获取加密服务
        /// </summary>
        private static IEncryptionServices GetEncryptionFromType(EncryptionType encryptionType)
        {
            return encryptionType switch
            {
                EncryptionType.FileOffSet => new FileOffsetEncryption(),
                EncryptionType.FileStream => new FileStreamEncryption(),
                _ => null
            };
        }

        /// <summary>
        /// 根据 ResourceModuleDriver 的 encryptionType 获取对应的加密服务（旧版兼容）
        /// </summary>
        private static IEncryptionServices GetEncryptionFromResourceModuleDriver()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab GameEntry");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[BuildInternal] Failed to find GameEntry.prefab");
                return null;
            }

            var gameEntryPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var gameEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gameEntryPath);
            if (gameEntryPrefab == null)
            {
                Debug.LogWarning("[BuildInternal] Failed to load GameEntry.prefab");
                return null;
            }

            var resourceModuleDriver = gameEntryPrefab.GetComponentInChildren<ResourceModuleDriver>();
            if (resourceModuleDriver == null)
            {
                Debug.LogWarning("[BuildInternal] ResourceModuleDriver not found in GameEntry.prefab");
                return null;
            }

            var encryptionType = resourceModuleDriver.EncryptionType;
            Debug.Log($"[BuildInternal] Use EncryptionType from ResourceModuleDriver: {encryptionType}");

            return GetEncryptionFromType(encryptionType);
        }

        private static string GetBuildPackageVersion()
        {
            int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
        }

        #endregion
    }
}
