using System.IO;
using UnityEngine;

namespace LevelEditor
{
    public static class LevelTools
    {
        // ================ 编辑器菜单（仅 Editor 下生效） ================

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/缓存/清除 PlayerPrefs")]
        private static void ClearPlayerPrefs()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog("清除 PlayerPrefs", "确认清除所有 PlayerPrefs 数据？", "确认", "取消"))
                return;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[缓存] PlayerPrefs 已清除");
        }

        [UnityEditor.MenuItem("Tools/缓存/清除游戏缓存")]
        private static void ClearGameCache()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog("清除游戏缓存", "确认清除游戏缓存文件？", "确认", "取消"))
                return;
            string filePath = Path.Combine(Application.persistentDataPath, "game_cache.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[缓存] 游戏缓存文件已删除: {filePath}");
            }
            else
            {
                Debug.Log("[缓存] 没有找到游戏缓存文件");
            }
        }

        [UnityEditor.MenuItem("Tools/缓存/清除所有缓存")]
        private static void ClearAllCache()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog("清除所有缓存", "确认清除 PlayerPrefs 和游戏缓存？", "确认", "取消"))
                return;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            string filePath = Path.Combine(Application.persistentDataPath, "game_cache.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            Debug.Log("[缓存] 所有缓存已清除");
        }
#endif
    }
}