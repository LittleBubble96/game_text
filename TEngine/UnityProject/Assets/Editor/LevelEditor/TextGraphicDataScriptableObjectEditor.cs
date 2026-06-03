using GameLogic.Data;
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    [CustomEditor(typeof(TextGraphicDataScriptableObject))]
    public class TextGraphicDataScriptableObjectEditor : UnityEditor.Editor
    {
        private string _inputText = "";
        // 在Inspector面板上显示一个按钮 和一个输入单个文字框，点击后按钮后调用TextGraphicDataScriptableObject的Generate方法
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            TextGraphicDataScriptableObject data = (TextGraphicDataScriptableObject)target;
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("输入单个文字：");
            _inputText = GUILayout.TextField(_inputText);
            if (GUILayout.Button("生成"))
            {
                string error = data.Generate(_inputText);
                if (!string.IsNullOrEmpty(error))
                {
                    EditorUtility.DisplayDialog("错误", error, "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("成功", "生成成功！", "确定");
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重新生成"))
            {
                data.ReGenerate();
            }
            GUILayout.EndHorizontal();
        }
    }
}