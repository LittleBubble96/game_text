using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Data;
using GameLogic.View;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace GameLogic.Data
{


// ==================== 数据模型 ====================

    /// <summary>
    /// 单个答案：用基字的哪些笔画可以组成目标字，
    /// 同一个目标字可能对应多组不同的笔画组合
    /// </summary>
    [Serializable]
    public class LevelAnswer
    {
        [Tooltip("目标字符，例如'一'")] public string answerCharacter;

        [Tooltip("多组笔画组合，每组都是一个可行的构成方式")] public List<StrokeSet> strokeSets = new List<StrokeSet>();
    }

    /// <summary>
    /// 一组笔画索引（一个可行的构成方式）
    /// </summary>
    [Serializable]
    public class StrokeSet
    {
        [Tooltip("使用基字的哪些笔画索引")] public List<int> strokeIndices = new List<int>();
    }

    /// <summary>
    /// 一个关卡：基字 + 多个答案
    /// </summary>
    [Serializable]
    public class TextLevelData
    {
        [Tooltip("关卡编号")] public int level;

        [Tooltip("基字，例如'树'")] public string baseCharacter;

        //位置偏移
        [Tooltip("位置偏移")] public Vector2 positionOffset = new Vector2(-2, -1);

        [Tooltip("所有可从基字中找到的答案")] public List<LevelAnswer> answers = new List<LevelAnswer>();

        [Tooltip("通关所需答案个数（0表示需要全部答对）")] public int requiredAnswerCount = 0;
    }

// ==================== ScriptableObject ====================

    public class TextLevelDataScriptableObject : ScriptableObject
    {
        public List<TextLevelData> levelDataList = new List<TextLevelData>();

#if UNITY_EDITOR
        [MenuItem("Assets/Create/TextLevelDataScriptableObject")]
        public static void CreateAsset()
        {
            TextLevelDataScriptableObject asset = CreateInstance<TextLevelDataScriptableObject>();
            AssetDatabase.CreateAsset(asset, "Assets/Resources/Res/TextLevelDataScriptableObject.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
#endif
    }
}
// ==================== 关卡编辑器窗口 ====================

#if UNITY_EDITOR
public class TextLevelEditorWindow : EditorWindow
{
    private TextLevelDataScriptableObject _levelDataAsset;
    private TextGraphicDataScriptableObject _graphicDataAsset;

    private int _selectedLevelIndex = -1;
    private Vector2 _levelListScroll;
    private Vector2 _detailScroll;
    private Vector2 _answerListScroll;

    // 新增/编辑状态
    private string _newAnswerCharacter = "";
    private List<int> _newStrokeIndices = new List<int>();

    // 编辑状态：当前编辑哪个答案的哪组笔画 (-1表示无，-2表示新增模式)
    private int _editingAnswerIndex = -1;
    private int _editingSetIndex = -1;

    private List<string> _availableCharacters = new List<string>();
    private Dictionary<string, int> _characterStrokeCount = new Dictionary<string, int>();

    // 笔画高亮状态
    private bool _showStrokeHighlightFoldout = false;
    private List<int> _highlightedStrokeIndices = new List<int>();

    [MenuItem("Tools/关卡编辑器")]
    public static void ShowWindow()
    {
        TextLevelEditorWindow window = GetWindow<TextLevelEditorWindow>("关卡编辑器");
        window.minSize = new Vector2(650, 500);
    }

    private void OnEnable()
    {
        RefreshAssets();
    }

    private void RefreshAssets()
    {
        _levelDataAsset = AssetDatabase.LoadAssetAtPath<TextLevelDataScriptableObject>(
            "Assets/Resources/Res/TextLevelDataScriptableObject.asset");
        _graphicDataAsset = AssetDatabase.LoadAssetAtPath<TextGraphicDataScriptableObject>(
            "Assets/Resources/Res/TextGraphicDataScriptableObject.asset");

        _availableCharacters.Clear();
        _characterStrokeCount.Clear();
        if (_graphicDataAsset != null && _graphicDataAsset.TextGraphicDataList != null)
        {
            foreach (var gd in _graphicDataAsset.TextGraphicDataList)
            {
                if (gd == null || string.IsNullOrEmpty(gd.character)) continue;
                _availableCharacters.Add(gd.character);
                _characterStrokeCount[gd.character] = gd.strokes.Count;
            }
        }
    }

    private void OnGUI()
    {
        if (_levelDataAsset == null)
        {
            EditorGUILayout.HelpBox("未找到关卡数据资产。请先通过 Assets > Create > TextLevelDataScriptableObject 创建。", MessageType.Warning);
            return;
        }

        GUILayout.Label("关卡编辑器", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新数据", GUILayout.Width(100)))
            RefreshAssets();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        // 左栏 - 关卡列表
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        DrawLevelList();
        EditorGUILayout.EndVertical();

        // 分隔线
        EditorGUILayout.BeginVertical(GUILayout.Width(2));
        GUILayout.Box("", GUILayout.ExpandHeight(true), GUILayout.Width(2));
        EditorGUILayout.EndVertical();

        // 右栏 - 关卡详情
        EditorGUILayout.BeginVertical();
        DrawLevelDetail();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    // ==================== 关卡列表 ====================

    private void DrawLevelList()
    {
        GUILayout.Label("关卡列表", EditorStyles.boldLabel);

        if (GUILayout.Button("+ 新建关卡"))
        {
            TextLevelData newLevel = new TextLevelData();
            newLevel.level = _levelDataAsset.levelDataList.Count + 1;
            _levelDataAsset.levelDataList.Add(newLevel);
            _selectedLevelIndex = _levelDataAsset.levelDataList.Count - 1;
            ResetEditState();
            EditorUtility.SetDirty(_levelDataAsset);
        }

        EditorGUILayout.Space();

        _levelListScroll = EditorGUILayout.BeginScrollView(_levelListScroll);
        for (int i = 0; i < _levelDataAsset.levelDataList.Count; i++)
        {
            TextLevelData level = _levelDataAsset.levelDataList[i];
            Color bgColor = (i == _selectedLevelIndex) ? new Color(0.3f, 0.5f, 0.8f, 0.5f) : GUI.backgroundColor;

            GUI.backgroundColor = bgColor;
            string label = string.IsNullOrEmpty(level.baseCharacter)
                ? $"关卡 {level.level} (未设置)"
                : $"关卡 {level.level}: {level.baseCharacter}";
            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                _selectedLevelIndex = i;
                ResetEditState();
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndScrollView();
    }

    // ==================== 关卡详情 ====================

    private void DrawLevelDetail()
    {
        if (_selectedLevelIndex < 0 || _selectedLevelIndex >= _levelDataAsset.levelDataList.Count)
        {
            GUILayout.Label("请从左侧选择一个关卡", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        TextLevelData level = _levelDataAsset.levelDataList[_selectedLevelIndex];

        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

        GUILayout.Label($"关卡 {level.level} 详情", EditorStyles.boldLabel);

        // 关卡编号
        EditorGUI.BeginChangeCheck();
        level.level = EditorGUILayout.IntField("关卡编号", level.level);
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_levelDataAsset);

        // 基字选择
        EditorGUI.BeginChangeCheck();
        int currentBaseIndex = _availableCharacters.IndexOf(level.baseCharacter);
        if (currentBaseIndex < 0 && !string.IsNullOrEmpty(level.baseCharacter))
            currentBaseIndex = 0;

        int newBaseIndex = EditorGUILayout.Popup("基字 (Base)", currentBaseIndex, _availableCharacters.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            string oldBase = level.baseCharacter;
            level.baseCharacter = newBaseIndex >= 0 ? _availableCharacters[newBaseIndex] : "";
            if (level.baseCharacter != oldBase)
            {
                foreach (var ans in level.answers)
                    ans.strokeSets.Clear();
            }
            EditorUtility.SetDirty(_levelDataAsset);
        }

        // 位置偏移
        EditorGUI.BeginChangeCheck();
        level.positionOffset = EditorGUILayout.Vector2Field("位置偏移", level.positionOffset);
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_levelDataAsset);

        if (!string.IsNullOrEmpty(level.baseCharacter) && _characterStrokeCount.ContainsKey(level.baseCharacter))
        {
            int strokeCount = _characterStrokeCount[level.baseCharacter];
            EditorGUILayout.HelpBox($"基字『{level.baseCharacter}』共有 {strokeCount} 个笔画（索引 0 ~ {strokeCount - 1}）", MessageType.Info);
            // 在场景中渲染基字（手动触发）
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("在场景渲染基字", GUILayout.Width(160)))
            {
                RenderBaseCharacterInScene(level.baseCharacter);
            }
            EditorGUILayout.EndHorizontal();

            // ===== 笔画索引高亮区域 =====
            EditorGUILayout.Space();
            _showStrokeHighlightFoldout = EditorGUILayout.Foldout(_showStrokeHighlightFoldout, "笔画索引高亮", true);
            if (_showStrokeHighlightFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("点击下方笔画索引按钮，场景中对应的笔画将变为黄色", MessageType.Info);

                DrawStrokeHighlightButtons(strokeCount);

                // 重置高亮按钮
                if (_highlightedStrokeIndices.Count > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.3f);
                    if (GUILayout.Button("取消高亮", GUILayout.Width(100)))
                    {
                        ResetStrokeHighlight();
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
        }

        EditorGUILayout.Space();

        // ===== 新增笔画组合区域 =====
        GUILayout.Label("添加笔画组合", EditorStyles.boldLabel);

        // 目标字输入
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("目标字:", GUILayout.Width(50));
        _newAnswerCharacter = EditorGUILayout.TextField(_newAnswerCharacter, GUILayout.Width(80));
        GUILayout.Label("笔画索引:", GUILayout.Width(60));
        string strokeInput = EditorGUILayout.TextField(string.Join(",", _newStrokeIndices), GUILayout.ExpandWidth(true));
        ParseStrokeInput(strokeInput);

        if (GUILayout.Button("+ 添加", GUILayout.Width(60)))
        {
            AddStrokeSet(level);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("笔画索引用逗号分隔（支持中文/英文逗号）。如目标字已有答案，将追加为新组合；否则新建答案。", MessageType.None);

        EditorGUILayout.Space();

        // ===== 答案汇总 =====
        if (level.answers.Count > 0)
        {
            string summary = string.Join(", ", level.answers.Select(a => $"【{a.answerCharacter}】"));
            EditorGUILayout.LabelField($"当前已有 {level.answers.Count} 个答案: {summary}", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
        }

        // ===== 答案列表 =====
        GUILayout.Label("答案详情", EditorStyles.boldLabel);

        _answerListScroll = EditorGUILayout.BeginScrollView(_answerListScroll, GUILayout.MinHeight(200));
        for (int ansIdx = 0; ansIdx < level.answers.Count; ansIdx++)
        {
            LevelAnswer answer = level.answers[ansIdx];
            DrawAnswerItem(level, ansIdx, answer);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("删除此关卡", GUILayout.Width(100)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确认删除关卡 {level.level}: {level.baseCharacter}？", "删除", "取消"))
            {
                _levelDataAsset.levelDataList.RemoveAt(_selectedLevelIndex);
                _selectedLevelIndex = -1;
                ResetEditState();
                EditorUtility.SetDirty(_levelDataAsset);
            }
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("保存", GUILayout.Width(100)))
        {
            EditorUtility.SetDirty(_levelDataAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("关卡数据已保存");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    private void DrawAnswerItem(TextLevelData level, int ansIdx, LevelAnswer answer)
    {
        // 答案标题行
        EditorGUILayout.BeginHorizontal();

        bool isEditingThisAnswer = (_editingAnswerIndex == ansIdx && _editingSetIndex == -1);
        GUI.backgroundColor = isEditingThisAnswer ? new Color(0.3f, 0.7f, 0.3f, 0.3f) : new Color(0.15f, 0.15f, 0.15f, 0.5f);
        EditorGUILayout.LabelField($"【{answer.answerCharacter}】({answer.strokeSets.Count} 种组合)", EditorStyles.whiteLabel, GUILayout.MinWidth(120));
        GUI.backgroundColor = Color.white;

        // 高亮按钮
        bool isAnswerHighlighted = IsAnswerHighlighted(answer);
        GUI.backgroundColor = isAnswerHighlighted ? Color.yellow : new Color(0.5f, 0.5f, 0.15f);
        if (GUILayout.Button("高亮", GUILayout.Width(40)))
        {
            if (isAnswerHighlighted)
                ResetStrokeHighlight();
            else
                HighlightAnswerStrokes(answer);
        }
        GUI.backgroundColor = Color.white;

        // 编辑该答案字符
        if (GUILayout.Button("改名", GUILayout.Width(40)))
        {
            _editingAnswerIndex = ansIdx;
            _editingSetIndex = -1;
            _newAnswerCharacter = answer.answerCharacter;
            _newStrokeIndices.Clear();
        }

        // 删除整个答案
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("删", GUILayout.Width(30)))
        {
            if (EditorUtility.DisplayDialog("确认", $"删除答案【{answer.answerCharacter}】及其所有 {answer.strokeSets.Count} 种组合？", "删除", "取消"))
            {
                level.answers.RemoveAt(ansIdx);
                ResetEditState();
                EditorUtility.SetDirty(_levelDataAsset);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // 如果正在改名此答案
        if (_editingAnswerIndex == ansIdx && _editingSetIndex == -1)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            _newAnswerCharacter = EditorGUILayout.TextField("新字符:", _newAnswerCharacter, GUILayout.Width(130));
            if (GUILayout.Button("确认", GUILayout.Width(50)))
            {
                answer.answerCharacter = _newAnswerCharacter;
                ResetEditState();
                EditorUtility.SetDirty(_levelDataAsset);
            }
            if (GUILayout.Button("取消", GUILayout.Width(50)))
            {
                ResetEditState();
            }
            EditorGUILayout.EndHorizontal();
        }

        // 列出所有笔画组合
        for (int setIdx = 0; setIdx < answer.strokeSets.Count; setIdx++)
        {
            StrokeSet set = answer.strokeSets[setIdx];
            bool isEditingThisSet = (_editingAnswerIndex == ansIdx && _editingSetIndex == setIdx);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();

            GUI.backgroundColor = isEditingThisSet ? new Color(0.3f, 0.7f, 0.3f, 0.3f) : Color.white;

            string setLabel = $"组合{setIdx + 1}: [{string.Join(", ", set.strokeIndices)}]";
            if (GUILayout.Button(setLabel, GUILayout.ExpandWidth(true)))
            {
                if (isEditingThisSet)
                {
                    ResetEditState();
                }
                else
                {
                    _editingAnswerIndex = ansIdx;
                    _editingSetIndex = setIdx;
                    _newStrokeIndices = new List<int>(set.strokeIndices);
                    _newAnswerCharacter = answer.answerCharacter;
                }
            }

            GUI.backgroundColor = Color.white;

            // 删除单组
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                answer.strokeSets.RemoveAt(setIdx);
                // 如果删光了，移除整个答案
                if (answer.strokeSets.Count == 0)
                    level.answers.RemoveAt(ansIdx);
                ResetEditState();
                EditorUtility.SetDirty(_levelDataAsset);
                GUI.backgroundColor = Color.white;
                break;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // 编辑这组笔画
            if (isEditingThisSet)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                string editInput = EditorGUILayout.TextField(string.Join(",", _newStrokeIndices), GUILayout.ExpandWidth(true));
                ParseStrokeInput(editInput);
                if (GUILayout.Button("应用", GUILayout.Width(50)))
                {
                    set.strokeIndices = new List<int>(_newStrokeIndices);
                    ResetEditState();
                    EditorUtility.SetDirty(_levelDataAsset);
                }
                if (GUILayout.Button("取消", GUILayout.Width(50)))
                {
                    ResetEditState();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(4);
    }

    // ==================== 辅助方法 ====================

    private void ParseStrokeInput(string input)
    {
        _newStrokeIndices.Clear();
        if (string.IsNullOrWhiteSpace(input)) return;

        string[] parts = input.Split(new[] { ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (int.TryParse(part.Trim(), out int index) && index >= 0)
                _newStrokeIndices.Add(index);
        }
    }

    private void AddStrokeSet(TextLevelData level)
    {
        if (string.IsNullOrEmpty(_newAnswerCharacter))
        {
            EditorUtility.DisplayDialog("提示", "请输入目标字字符", "确定");
            return;
        }
        if (_newStrokeIndices.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请输入至少一个笔画索引", "确定");
            return;
        }

        // 查找是否已有相同目标字的答案
        LevelAnswer existing = level.answers.Find(a => a.answerCharacter == _newAnswerCharacter);
        if (existing != null)
        {
            // 已有该答案，追加新组合
            existing.strokeSets.Add(new StrokeSet
            {
                strokeIndices = new List<int>(_newStrokeIndices)
            });
        }
        else
        {
            // 新答案
            LevelAnswer newAnswer = new LevelAnswer
            {
                answerCharacter = _newAnswerCharacter,
                strokeSets = new List<StrokeSet>
                {
                    new StrokeSet { strokeIndices = new List<int>(_newStrokeIndices) }
                }
            };
            level.answers.Add(newAnswer);
        }

        _newAnswerCharacter = "";
        _newStrokeIndices.Clear();
        EditorUtility.SetDirty(_levelDataAsset);
    }

    private void DrawStrokeHighlightButtons(int strokeCount)
    {
        // 每行最多显示按钮数
        const int maxPerRow = 8;
        for (int rowStart = 0; rowStart < strokeCount; rowStart += maxPerRow)
        {
            EditorGUILayout.BeginHorizontal();
            int rowEnd = Math.Min(rowStart + maxPerRow, strokeCount);
            for (int i = rowStart; i < rowEnd; i++)
            {
                bool isHighlighted = _highlightedStrokeIndices.Contains(i);
                GUI.backgroundColor = isHighlighted ? Color.yellow : Color.white;
                if (GUILayout.Button(i.ToString(), GUILayout.Width(35), GUILayout.Height(25)))
                {
                    if (isHighlighted)
                    {
                        _highlightedStrokeIndices.Remove(i);
                        ApplyHighlightToScene();
                    }
                    else
                    {
                        _highlightedStrokeIndices.Add(i);
                        ApplyHighlightToScene();
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void HighlightAnswerStrokes(LevelAnswer answer)
    {
        _highlightedStrokeIndices.Clear();
        foreach (var set in answer.strokeSets)
        {
            foreach (int idx in set.strokeIndices)
            {
                if (!_highlightedStrokeIndices.Contains(idx))
                    _highlightedStrokeIndices.Add(idx);
            }
        }
        _highlightedStrokeIndices.Sort();
        ApplyHighlightToScene();
    }

    private bool IsAnswerHighlighted(LevelAnswer answer)
    {
        if (_highlightedStrokeIndices.Count == 0) return false;

        // 收集该答案的所有笔画索引
        HashSet<int> answerIndices = new HashSet<int>();
        foreach (var set in answer.strokeSets)
        {
            foreach (int idx in set.strokeIndices)
                answerIndices.Add(idx);
        }

        // 检查是否完全匹配（高亮的笔画集合 == 答案的笔画集合）
        return answerIndices.SetEquals(_highlightedStrokeIndices);
    }

    private void ApplyHighlightToScene()
    {
        DrawCharacter drawer = GameObject.FindObjectOfType<DrawCharacter>();
        if (drawer == null)
        {
            Debug.LogWarning("场景中未找到 DrawCharacter");
            return;
        }

        drawer.ResetAllStrokeColors();
        foreach (int idx in _highlightedStrokeIndices)
        {
            drawer.SetStrokeColor(idx, Color.yellow);
        }

        if (_highlightedStrokeIndices.Count > 0)
            Debug.Log($"已高亮笔画索引: [{string.Join(", ", _highlightedStrokeIndices)}]");
    }

    private void ResetStrokeHighlight()
    {
        if (_highlightedStrokeIndices.Count == 0) return;

        _highlightedStrokeIndices.Clear();

        DrawCharacter drawer = GameObject.FindObjectOfType<DrawCharacter>();
        if (drawer != null)
        {
            drawer.ResetAllStrokeColors();
        }
    }

    private void ResetEditState()
    {
        _editingAnswerIndex = -1;
        _editingSetIndex = -1;
        _newAnswerCharacter = "";
        _newStrokeIndices.Clear();
        ResetStrokeHighlight();
    }

    private void RenderBaseCharacterInScene(string ch)
    {
        if (_graphicDataAsset == null)
        {
            EditorUtility.DisplayDialog("错误", "未加载图形数据，请先点击刷新数据", "确定");
            return;
        }

        var gd = _graphicDataAsset.TextGraphicDataList.Find(x => x.character == ch);
        if (gd == null)
        {
            EditorUtility.DisplayDialog("错误", $"字符 '{ch}' 在图形数据中未找到", "确定");
            return;
        }

        // 在场景中查找 DrawCharacter
        DrawCharacter drawer = GameObject.FindObjectOfType<DrawCharacter>();
        if (drawer == null)
        {
            EditorUtility.DisplayDialog("错误", "场景中未找到 DrawCharacter 组件，请在 Scene 中创建并绑定 DrawCharacter。", "确定");
            return;
        }

        // 调用绘制（Clear 中已处理 Editor 下的 DestroyImmediate 调用时序）
        drawer.Clear();

        // 应用关卡的位置偏移
        TextLevelData currentLevel = _levelDataAsset.levelDataList[_selectedLevelIndex];
        drawer.PositionOffset = currentLevel.positionOffset;

        drawer.Draw(gd, showStrokeIndices: true);

        // 标记场景已修改
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"已在场景中渲染基字 '{ch}'");
    }
}
#endif
