using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 构建过程日志文件写入器。
    /// <remarks>切换宏 / AssetDatabase.Refresh 会触发脚本重编译并清空 Unity Console，导致构建日志丢失。
    /// 本写入器通过 Application.logMessageReceived 把日志实时落盘，Console 被清空也不丢失。</remarks>
    /// <remarks>回调可能来自非主线程，写文件加锁；StreamWriter 开 AutoFlush 保证崩溃前已写入磁盘。</remarks>
    /// </summary>
    public static class BuildLogger
    {
        /// <summary>
        /// 日志默认输出根目录（项目根下 Builds/Logs）。
        /// </summary>
        public static string DefaultLogRoot =>
            Path.GetFullPath(Application.dataPath + "/../Builds/Logs").Replace('\\', '/');

        /// <summary>
        /// 开始记录构建日志，返回一个 IDisposable 作用域；作用域 Dispose 时注销回调并关闭文件。
        /// </summary>
        /// <param name="logPath">日志文件绝对路径；为空则自动生成。</param>
        public static IDisposable Begin(string logPath = null)
        {
            return new BuildLogScope(logPath);
        }

        /// <summary>
        /// 按平台/模式生成日志文件绝对路径，并确保目录存在。
        /// </summary>
        /// <param name="target">构建目标平台。</param>
        /// <param name="mode">构建模式（如 Release/Develop），用于文件名标识。</param>
        /// <param name="root">日志根目录；为空则用 <see cref="DefaultLogRoot"/>。</param>
        public static string GetLogPath(BuildTarget target, string mode, string root = null)
        {
            string logRoot = string.IsNullOrEmpty(root)
                ? DefaultLogRoot
                : Path.GetFullPath(root).Replace('\\', '/');

            if (!Directory.Exists(logRoot))
            {
                Directory.CreateDirectory(logRoot);
            }

            string time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string modePart = string.IsNullOrEmpty(mode) ? "Build" : mode;
            string fileName = $"Build_{target}_{modePart}_{time}.log";
            return Path.Combine(logRoot, fileName).Replace('\\', '/');
        }

        /// <summary>
        /// 构建日志作用域：注册 logMessageReceived 实时写文件，Dispose 注销并关闭文件。
        /// </summary>
        private sealed class BuildLogScope : IDisposable
        {
            private readonly StreamWriter _writer;
            private readonly Application.LogCallback _callback;
            private readonly object _lock = new object();
            private bool _disposed;

            public BuildLogScope(string logPath)
            {
                string path = string.IsNullOrEmpty(logPath)
                    ? GetLogPath(BuildTarget.NoTarget, "Build")
                    : logPath;

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _writer = new StreamWriter(path, append: false, encoding: new UTF8Encoding(false))
                {
                    AutoFlush = true
                };

                _callback = OnLogReceived;
                Application.logMessageReceived += _callback;

                Write($"========== 构建日志开始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==========");
                Write($"日志文件: {path}");
            }

            private void OnLogReceived(string condition, string stackTrace, LogType type)
            {
                string level = type switch
                {
                    LogType.Error => "ERR",
                    LogType.Assert => "ASSERT",
                    LogType.Warning => "WARN",
                    LogType.Exception => "EXC",
                    LogType.Log => "LOG",
                    _ => "LOG"
                };

                string line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {condition}";
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    line += Environment.NewLine + stackTrace;
                }
                Write(line);
            }

            private void Write(string message)
            {
                lock (_lock)
                {
                    if (_disposed || _writer == null)
                    {
                        return;
                    }
                    _writer.WriteLine(message);
                }
            }

            public void Dispose()
            {
                // 先注销回调，避免注销期间仍有 in-flight 回调竞争写文件
                Application.logMessageReceived -= _callback;

                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }
                    _disposed = true;
                    Write($"========== 构建日志结束 {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==========");
                    _writer.Flush();
                    _writer.Dispose();
                }
            }
        }
    }
}
