using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace TEngine.Editor
{
    /// <summary>
    /// 脚本宏定义操作类。
    /// </summary>
    public static class ScriptingDefineSymbols
    {
        private static readonly BuildTargetGroup[] BuildTargetGroups = new BuildTargetGroup[]
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.iOS,
            BuildTargetGroup.Android,
            BuildTargetGroup.WSA,
            BuildTargetGroup.WebGL
        };

        /// <summary>
        /// 检查指定平台是否存在指定的脚本宏定义。
        /// </summary>
        /// <param name="buildTargetGroup">要检查脚本宏定义的平台。</param>
        /// <param name="scriptingDefineSymbol">要检查的脚本宏定义。</param>
        /// <returns>指定平台是否存在指定的脚本宏定义。</returns>
        public static bool HasScriptingDefineSymbol(BuildTargetGroup buildTargetGroup, string scriptingDefineSymbol)
        {
            if (string.IsNullOrEmpty(scriptingDefineSymbol))
            {
                return false;
            }

            string[] scriptingDefineSymbols = GetScriptingDefineSymbols(buildTargetGroup);
            foreach (string i in scriptingDefineSymbols)
            {
                if (i == scriptingDefineSymbol)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 为指定平台增加指定的脚本宏定义。
        /// </summary>
        /// <param name="buildTargetGroup">要增加脚本宏定义的平台。</param>
        /// <param name="scriptingDefineSymbol">要增加的脚本宏定义。</param>
        public static void AddScriptingDefineSymbol(BuildTargetGroup buildTargetGroup, string scriptingDefineSymbol)
        {
            if (string.IsNullOrEmpty(scriptingDefineSymbol))
            {
                return;
            }

            if (HasScriptingDefineSymbol(buildTargetGroup, scriptingDefineSymbol))
            {
                return;
            }

            List<string> scriptingDefineSymbols = new List<string>(GetScriptingDefineSymbols(buildTargetGroup))
            {
                scriptingDefineSymbol
            };

            SetScriptingDefineSymbols(buildTargetGroup, scriptingDefineSymbols.ToArray());
        }

        /// <summary>
        /// 为指定平台移除指定的脚本宏定义。
        /// </summary>
        /// <param name="buildTargetGroup">要移除脚本宏定义的平台。</param>
        /// <param name="scriptingDefineSymbol">要移除的脚本宏定义。</param>
        public static void RemoveScriptingDefineSymbol(BuildTargetGroup buildTargetGroup, string scriptingDefineSymbol)
        {
            if (string.IsNullOrEmpty(scriptingDefineSymbol))
            {
                return;
            }

            if (!HasScriptingDefineSymbol(buildTargetGroup, scriptingDefineSymbol))
            {
                return;
            }

            List<string> scriptingDefineSymbols = new List<string>(GetScriptingDefineSymbols(buildTargetGroup));
            while (scriptingDefineSymbols.Contains(scriptingDefineSymbol))
            {
                scriptingDefineSymbols.Remove(scriptingDefineSymbol);
            }

            SetScriptingDefineSymbols(buildTargetGroup, scriptingDefineSymbols.ToArray());
        }

        /// <summary>
        /// 为所有平台增加指定的脚本宏定义。
        /// </summary>
        /// <param name="scriptingDefineSymbol">要增加的脚本宏定义。</param>
        public static void AddScriptingDefineSymbol(string scriptingDefineSymbol)
        {
            if (string.IsNullOrEmpty(scriptingDefineSymbol))
            {
                return;
            }

            foreach (BuildTargetGroup buildTargetGroup in BuildTargetGroups)
            {
                AddScriptingDefineSymbol(buildTargetGroup, scriptingDefineSymbol);
            }
        }

        /// <summary>
        /// 备份所有受管平台的脚本宏定义，返回平台到宏数组的映射（用于打包后恢复）。
        /// </summary>
        public static Dictionary<BuildTargetGroup, string[]> BackupAllPlatformDefines()
        {
            var backup = new Dictionary<BuildTargetGroup, string[]>();
            foreach (BuildTargetGroup buildTargetGroup in BuildTargetGroups)
            {
                backup[buildTargetGroup] = GetScriptingDefineSymbols(buildTargetGroup);
            }
            return backup;
        }

        /// <summary>
        /// 用指定宏集合覆盖所有受管平台的脚本宏定义（打包前调用）。
        /// </summary>
        /// <param name="defines">要覆盖的宏集合。</param>
        public static void OverrideAllPlatformDefines(string[] defines)
        {
            foreach (BuildTargetGroup buildTargetGroup in BuildTargetGroups)
            {
                SetScriptingDefineSymbols(buildTargetGroup, defines ?? System.Array.Empty<string>());
            }
        }

        /// <summary>
        /// 用备份的平台宏映射恢复脚本宏定义（打包后调用）。
        /// </summary>
        /// <param name="backup">BackupAllPlatformDefines 返回的备份。</param>
        public static void RestoreAllPlatformDefines(Dictionary<BuildTargetGroup, string[]> backup)
        {
            if (backup == null)
            {
                return;
            }

            foreach (BuildTargetGroup buildTargetGroup in BuildTargetGroups)
            {
                backup.TryGetValue(buildTargetGroup, out string[] defines);
                SetScriptingDefineSymbols(buildTargetGroup, defines ?? System.Array.Empty<string>());
            }
        }

        /// <summary>
        /// 为所有平台移除指定的脚本宏定义。
        /// </summary>
        /// <param name="scriptingDefineSymbol">要移除的脚本宏定义。</param>
        public static void RemoveScriptingDefineSymbol(string scriptingDefineSymbol)
        {
            if (string.IsNullOrEmpty(scriptingDefineSymbol))
            {
                return;
            }

            foreach (BuildTargetGroup buildTargetGroup in BuildTargetGroups)
            {
                RemoveScriptingDefineSymbol(buildTargetGroup, scriptingDefineSymbol);
            }
        }

        /// <summary>
        /// 获取指定平台的脚本宏定义。
        /// </summary>
        /// <param name="buildTargetGroup">要获取脚本宏定义的平台。</param>
        /// <returns>平台的脚本宏定义。</returns>
        public static string[] GetScriptingDefineSymbols(BuildTargetGroup buildTargetGroup)
        {
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup), out var result);
            return result;
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup).Split(';');
#endif
        }

        /// <summary>
        /// 设置指定平台的脚本宏定义。
        /// </summary>
        /// <param name="buildTargetGroup">要设置脚本宏定义的平台。</param>
        /// <param name="scriptingDefineSymbols">要设置的脚本宏定义。</param>
        public static void SetScriptingDefineSymbols(BuildTargetGroup buildTargetGroup, string[] scriptingDefineSymbols)
        {
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup), scriptingDefineSymbols);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";", scriptingDefineSymbols));
#endif
        }

        /// <summary>
        /// 仅对指定平台设置脚本宏定义（覆盖该平台原有宏）并刷新资源库。
        /// <para>相比 <see cref="OverrideAllPlatformDefines"/> 只影响目标平台，不会改动其他平台；
        /// 且不自动备份/恢复，调用方需自行决定是否在打包后恢复原值。</para>
        /// </summary>
        /// <param name="targetGroup">要设置脚本宏定义的目标平台。</param>
        /// <param name="defines">要覆盖写入的宏集合。</param>
        public static void SetDefines(BuildTargetGroup targetGroup, string[] defines)
        {
            SetScriptingDefineSymbols(targetGroup, defines ?? System.Array.Empty<string>());
            AssetDatabase.Refresh();
        }
    }
}
