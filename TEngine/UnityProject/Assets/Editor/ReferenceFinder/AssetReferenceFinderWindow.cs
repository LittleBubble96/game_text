using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Object = UnityEngine.Object;

public class AssetReferenceFinderWindow : EditorWindow
{
    private Object targetAsset;
    private Vector2 scrollPosition;
    private Dictionary<string, List<Object>> groupedReferences;
    private Dictionary<string, bool> foldoutStates;
    private bool haveClickedFind = false;

    [MenuItem("Tools/资源引用查找器")]
    public static void ShowWindow()
    {
        GetWindow<AssetReferenceFinderWindow>("资源引用查找器");
    }

    private void OnEnable()
    {
        groupedReferences = new Dictionary<string, List<Object>>();
        foldoutStates = new Dictionary<string, bool>();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("选择一个资源以查找其引用", EditorStyles.boldLabel);
        
        // 资源选择区域
        EditorGUILayout.BeginHorizontal();
        Object newTargetAsset = EditorGUILayout.ObjectField("目标资源", targetAsset, typeof(Object), false);
        if (newTargetAsset != targetAsset)
        {
            targetAsset = newTargetAsset;
            haveClickedFind = false;
            groupedReferences.Clear();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("刷新"))
        {
            if (targetAsset != null)
            {
                FindReferences();
            }
        }

        EditorGUILayout.Space(10);

        // 显示结果
        if (groupedReferences.Count > 0)
        {
            EditorGUILayout.LabelField($"共查找到 {GetTotalReferencesCount()}条 引用", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var group in groupedReferences.OrderBy(g => g.Key))
            {
                foldoutStates.TryAdd(group.Key, false);

                EditorGUILayout.Space(5);
                foldoutStates[group.Key] = EditorGUILayout.Foldout(foldoutStates[group.Key], $"{group.Key} ({group.Value.Count})");

                if (foldoutStates[group.Key])
                {
                    EditorGUI.indentLevel++;
                    foreach (var reference in group.Value)
                    {
                        EditorGUILayout.ObjectField(reference, reference.GetType(), false);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }
        else if (targetAsset != null)
        {
            if(haveClickedFind) EditorGUILayout.HelpBox("未发现任何引用", MessageType.Info);
            else EditorGUILayout.HelpBox("请点击刷新", MessageType.Info);
        }
    }

    private void FindReferences()
    {
        haveClickedFind = true;
        groupedReferences.Clear();
        string targetPath = AssetDatabase.GetAssetPath(targetAsset);
        if (string.IsNullOrEmpty(targetPath)) return;

        // 获取所有资源的GUID
        string targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
        
        List<string> allAssets = AssetDatabase.GetAllAssetPaths()            
            .Where(path => path.StartsWith("Assets/")) // 跳过非资源文件
            .Where(path => !path.EndsWith(".json")) // 跳过json文件
            .Where(path => !path.EndsWith(".png")) // 跳过png文件
            .Where(path => !path.EndsWith(".cs")) // 跳过代码
            .ToList();

        int curNum = 0, totalNum = allAssets.Count;
        // 查找引用
        try
        {
            foreach (string assetPath in allAssets)
            {
                EditorUtility.DisplayProgressBar("查找资源引用", $"正在扫描{assetPath}", (float)curNum++ / totalNum);

                // 读取依赖关系
                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                if (dependencies.Contains(targetPath))
                {
                    // 如果不是目标资源本身
                    if (assetPath != targetPath)
                    {
                        Object reference = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                        if (reference != null)
                        {
                            string directory = Path.GetDirectoryName(assetPath);
                            if (!groupedReferences.ContainsKey(directory))
                            {
                                groupedReferences[directory] = new List<Object>();
                            }

                            groupedReferences[directory].Add(reference);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($" >>> 查询过程中产生错误 {e.Message}");
            EditorUtility.ClearProgressBar();
        }
        EditorUtility.ClearProgressBar();
    }

    private int GetTotalReferencesCount()
    {
        return groupedReferences.Sum(g => g.Value.Count);
    }
    
    [MenuItem("Assets/查找资源引用", false, 25)]
    private static void FindReferencesFromContext()
    {
        // 获取当前选中的资源
        Object selectedAsset = Selection.activeObject;
        if (selectedAsset == null) return;

        // 打开窗口并设置目标资源
        AssetReferenceFinderWindow window = GetWindow<AssetReferenceFinderWindow>("资源引用查找器");
        window.SetTargetAssetAndFind(selectedAsset);
    }

    // 添加这个方法来设置目标资源并立即执行查找
    private void SetTargetAssetAndFind(Object asset)
    {
        targetAsset = asset;
        if (targetAsset != null)
        {
            FindReferences();
        }
    }
}