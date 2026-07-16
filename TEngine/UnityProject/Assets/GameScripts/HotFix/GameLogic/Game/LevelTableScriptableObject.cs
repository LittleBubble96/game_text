using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameLogic.Data
{
    /// <summary>
    /// 关卡表条目：levelId（1开始）-> levelName
    /// 游戏通过此表按顺序映射到编辑器产生的关卡数据
    /// </summary>
    [Serializable]
    public class LevelTableEntry
    {
        [Tooltip("关卡ID，从1开始")] public int levelId = 1;
        [Tooltip("关卡名称，对应 TextLevelData 中的 levelName")] public string levelName;
    }

    public class LevelTableScriptableObject : ScriptableObject
    {
        public List<LevelTableEntry> entries = new List<LevelTableEntry>();

#if UNITY_EDITOR
        [MenuItem("Assets/Create/LevelTableScriptableObject")]
        public static void CreateAsset()
        {
            LevelTableScriptableObject asset = CreateInstance<LevelTableScriptableObject>();
            AssetDatabase.CreateAsset(asset, "Assets/AssetRaw/Configs/LevelConfigs/LevelTable.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
#endif
    }
}
