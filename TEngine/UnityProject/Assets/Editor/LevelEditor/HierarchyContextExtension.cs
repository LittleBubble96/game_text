#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace Level.Editor.Tools
{
    public static class HierarchyContextExtension
    {
        [MenuItem("GameObject/Copy Hierarchy Path", priority = -10)]
        private static void GetHierarchyPath()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                return;
            }

            string path = GetRelativePath(selectedObject);
            
            // 复制到剪贴板
            EditorGUIUtility.systemCopyBuffer = path;
        }

        /// <summary>
        /// 获取GameObject相对于根节点的层级路径
        /// 在预制体预览模式下去掉根物体，在Scene中返回完整路径
        /// </summary>
        private static string GetRelativePath(GameObject go)
        {
            if (go == null)
                return string.Empty;

            // 判断是否在预制体编辑模式
            bool isInPrefabEditMode = PrefabStageUtility.GetCurrentPrefabStage() != null;

            string path = go.name;
            Transform parent = go.transform.parent;
            Transform rootTransform = null;

            while (parent != null)
            {
                if (parent.name != "Canvas (Environment)")
                {
                    path = parent.name + "/" + path;
                }

                rootTransform = parent;
                parent = parent.parent;
            }

            // 如果在预制体编辑模式，去掉根物体
            if (isInPrefabEditMode && rootTransform != null && path.Contains("/"))
            {
                int firstSlashIndex = path.IndexOf("/");
                if (firstSlashIndex > 0)
                {
                    path = path.Substring(firstSlashIndex + 1);
                }
            }

            return path;
        }
    }
}
#endif