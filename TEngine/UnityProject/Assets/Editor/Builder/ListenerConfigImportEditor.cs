using TEngine.Editor;
using UnityEditor;

namespace Builder
{
    public class ListenerConfigImportEditor
    {
        [InitializeOnLoadMethod]
        public static void RegisterListener()
        {
            // 确保在编辑器加载时注册事件监听器
            LubanTools.BindExportFinish(OnExportFinish);
        }
        
        private static void OnExportFinish()
        {
            LanguageGenerateEditor.Generate();
        }
    }
}