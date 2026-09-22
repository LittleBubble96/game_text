using System;
using System.IO;
using System.Linq;
using TEngine.Editor;
using UnityEditor;
using UnityEngine;

namespace TEngine
{
    /// <summary>构建宏事务：修改前落盘，恢复后读回校验；重载或重启后恢复未完成的事务。</summary>
    internal sealed class BuildDefineScope : IDisposable
    {
        [Serializable]
        private sealed class Backup
        {
            public BuildTargetGroup Target;
            public string[] Defines;
        }

        private static string BackupPath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Library/TEngineBuildDefines.json"));
        private static bool _active;
        private bool _disposed;

        [InitializeOnLoadMethod]
        private static void ScheduleRecovery()
        {
            EditorApplication.delayCall += RecoverWhenIdle;
        }

        private static void RecoverWhenIdle()
        {
            if (_active || !File.Exists(BackupPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
            {
                EditorApplication.delayCall += RecoverWhenIdle;
                return;
            }
            try
            {
                RestorePending();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildDefines] 自动恢复失败，原宏备份保留在 {BackupPath}\n{e}");
            }
        }

        public static BuildDefineScope Begin(BuildTargetGroup target, string[] defines)
        {
            if (_active) throw new InvalidOperationException("已有构建正在使用临时宏，不能重复启动构建。");
            // 不允许下一次构建把上次残留的打包宏当成原宏覆盖备份。
            RestorePending();
            var backup = new Backup
            {
                Target = target,
                Defines = ScriptingDefineSymbols.GetScriptingDefineSymbols(target)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup, true));
            var scope = new BuildDefineScope();
            _active = true;
            try
            {
                Debug.Log($"[BuildDefines] 平台={target} 原宏=[{string.Join(";", backup.Defines)}] 打包宏=[{string.Join(";", defines)}]");
                ScriptingDefineSymbols.SetDefines(target, defines);
                Verify(target, defines);
                return scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                RestorePending();
            }
            finally
            {
                _active = false;
            }
        }

        private static void RestorePending()
        {
            if (!File.Exists(BackupPath)) return;
            var backup = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
            if (backup == null || backup.Defines == null || backup.Target == BuildTargetGroup.Unknown)
                throw new InvalidDataException($"宏备份无效，请检查 {BackupPath}");

            ScriptingDefineSymbols.SetDefines(backup.Target, backup.Defines);
            AssetDatabase.SaveAssets();
            Verify(backup.Target, backup.Defines);
            // 只有写入、保存和读回校验都成功后才移除恢复依据。
            File.Delete(BackupPath);
            Debug.Log($"[BuildDefines] 已读回校验恢复值：平台={backup.Target} 宏=[{string.Join(";", backup.Defines)}]");
        }

        private static void Verify(BuildTargetGroup target, string[] expected)
        {
            var actual = ScriptingDefineSymbols.GetScriptingDefineSymbols(target);
            // Unity 可能调整顺序、去重；空宏的读取结果也可能是单个空字符串。
            var expectedSet = new System.Collections.Generic.HashSet<string>(expected.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!expectedSet.SetEquals(actual.Where(s => !string.IsNullOrWhiteSpace(s))))
                throw new InvalidOperationException($"宏写入校验失败：平台={target} 期望=[{string.Join(";", expected)}] 实际=[{string.Join(";", actual)}]");
        }
    }
}
