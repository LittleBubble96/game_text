using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TEngine.Editor
{
    public static class LubanTools
    {
        private static Action _onExportFinish;
        
        [MenuItem("TEngine/Luban/转表 &X", priority = -100)]
        private static void ExportConfigs()
        {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            string path = Application.dataPath + "/../../Configs/GameConfig/gen_code_bin_to_project_lazyload.sh";
#elif UNITY_EDITOR_WIN
            string path = Application.dataPath + "/../../Configs/GameConfig/gen_code_bin_to_project_lazyload.bat";
#endif
            var startInfo = new ProcessStartInfo()
            {
                FileName = path,
            };
            using (var myProcess = Process.Start(startInfo))
            {
                if (myProcess != null)
                {
                    myProcess.WaitForExit();
                    var exitCode = myProcess.ExitCode;
                    Debug.Log($"Process Exit Code {exitCode}");
                }

                // Debug.Log($"执行转表：{path}");
                // ShellHelper.RunByPath(path);
                _onExportFinish?.Invoke();
            }
        }
        
        public static void BindExportFinish(Action onExportFinish)
        {
            _onExportFinish = onExportFinish;
        }
    }
}