using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Data;
using GameLogic.View;
using UnityEngine;

#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using hyjiacan.py4n;
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
        [Tooltip("关卡名称（唯一标识）")] public string levelName;

        [Tooltip("基字，例如'树'")] public string baseCharacter;

        //位置偏移
        [Tooltip("位置偏移")] public Vector2 positionOffset = new Vector2(-2, -1);

        [Tooltip("所有可从基字中找到的答案")] public List<LevelAnswer> answers = new List<LevelAnswer>();

        [Tooltip("通关所需答案个数（0表示需要全部答对）")] public int requiredAnswerCount = 0;

        /// <summary>
        /// 校验关卡数据是否有效。
        /// JsonUtility 可能将 JSON null 反序列化为默认空实例（baseCharacter=null），
        /// 此时需要用此方法做二次校验，而非仅靠 null 判断。
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(baseCharacter) && !string.IsNullOrEmpty(levelName) && answers != null;
        }
    }

    [Serializable]
    public struct TextToneData
    {
        public string character;
        public string tone;

        public TextToneData(string c, string t)
        {
            character = c;
            tone = t;
        }
    }

// ==================== ScriptableObject ====================

    public class TextLevelDataScriptableObject : ScriptableObject
    {
        public List<TextLevelData> levelDataList = new List<TextLevelData>();

        public List<TextToneData> characterToTone = new List<TextToneData>();

#if UNITY_EDITOR
        [MenuItem("Assets/Create/TextLevelDataScriptableObject")]
        public static void CreateAsset()
        {
            TextLevelDataScriptableObject asset = CreateInstance<TextLevelDataScriptableObject>();
            AssetDatabase.CreateAsset(asset, "Assets/AssetRaw/Configs/LevelConfigs/TextLevelDataScriptableObject.asset");
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
    private int _selectedExistingAnswerIndex = 0; // 0 = "-- 输入新字 --"
    private int _selectedCommonAnswerIndex = 0; // 0 = "-- 常用答案 --"

    // 编辑状态：当前编辑哪个答案的哪组笔画 (-1表示无，-2表示新增模式)
    private int _editingAnswerIndex = -1;
    private int _editingSetIndex = -1;

    // 替换关卡：当前选中关卡要与之交换的目标关卡索引
    private int _swapTargetIndex = -1;

    private List<string> _availableCharacters = new List<string>();
    private HashSet<string> _levelCharacters = new HashSet<string>();
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
            "Assets/AssetRaw/Configs/LevelConfigs/TextLevelDataScriptableObject.asset");
        _graphicDataAsset = AssetDatabase.LoadAssetAtPath<TextGraphicDataScriptableObject>(
            "Assets/AssetRaw/Configs/LevelConfigs/TextGraphicDataScriptableObject.asset");

        _levelCharacters.Clear();
        _availableCharacters.Clear();
        _characterStrokeCount.Clear();
        if (_levelDataAsset != null && _levelDataAsset.levelDataList != null)
        {
            foreach (var levelData in _levelDataAsset.levelDataList)
            {
                _levelCharacters.Add(levelData.baseCharacter);
            }
        }
        if (_graphicDataAsset != null && _graphicDataAsset.TextGraphicDataList != null)
        {
            foreach (var gd in _graphicDataAsset.TextGraphicDataList)
            {
                if (gd == null || string.IsNullOrEmpty(gd.character)) continue;
                if (_levelCharacters.Contains(gd.character))
                {
                    _availableCharacters.Add(gd.character);
                }
                else
                {
                    _availableCharacters.Insert(0 , gd.character);
                }
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

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 新建关卡"))
        {
            TextLevelData newLevel = new TextLevelData();
            // 根据现有 Level_N 的最大序号 +1 命名（无合法项时从 1 开始）
            newLevel.levelName = GenerateNextLevelName();
            _levelDataAsset.levelDataList.Add(newLevel);
            _selectedLevelIndex = _levelDataAsset.levelDataList.Count - 1;
            ResetEditState();
            EditorUtility.SetDirty(_levelDataAsset);
        }
        if (GUILayout.Button("重排关卡"))
        {
            if (EditorUtility.DisplayDialog("确认重排",
                "将按关卡名称（Level_序号）重新排序当前关卡列表，确认继续？",
                "重排", "取消"))
            {
                SortLevels();
            }
        }
        if (GUILayout.Button("刷新音调"))
        {
            RefreshTones();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        _levelListScroll = EditorGUILayout.BeginScrollView(_levelListScroll);
        for (int i = 0; i < _levelDataAsset.levelDataList.Count; i++)
        {
            TextLevelData level = _levelDataAsset.levelDataList[i];
            Color bgColor = (i == _selectedLevelIndex) ? new Color(0.3f, 0.5f, 0.8f, 0.5f) : GUI.backgroundColor;

            GUI.backgroundColor = bgColor;
            string label = string.IsNullOrEmpty(level.baseCharacter)
                ? $"{level.levelName} (未设置)"
                : $"{level.levelName}: {level.baseCharacter}";
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

        GUILayout.Label($"关卡 {level.levelName} 详情", EditorStyles.boldLabel);

        // 关卡名称（改名后校验重名，不自动排序；重名则回退原名并提示）
        EditorGUI.BeginChangeCheck();
        string newName = EditorGUILayout.TextField("关卡名称", level.levelName);
        if (EditorGUI.EndChangeCheck())
        {
            if (IsLevelNameDuplicated(newName, _selectedLevelIndex))
            {
                EditorUtility.DisplayDialog("关卡名称重复",
                    $"已存在名为『{newName}』的关卡，请使用其他名称。\n（如需调整顺序，请点击「重排关卡」）",
                    "确定");
            }
            else
            {
                level.levelName = newName;
                EditorUtility.SetDirty(_levelDataAsset);
            }
        }

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

        // 通关所需答案个数
        EditorGUI.BeginChangeCheck();
        level.requiredAnswerCount = EditorGUILayout.IntField("通关所需答案个数", level.requiredAnswerCount);
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_levelDataAsset);

        if (level.requiredAnswerCount <= 0)
        {
            EditorGUILayout.HelpBox("当前为 0，表示需要找出全部答案才能通关。", MessageType.Info);
        }
        else if (level.requiredAnswerCount > level.answers.Count)
        {
            EditorGUILayout.HelpBox($"所需答案数 ({level.requiredAnswerCount}) 超出了实际答案总数 ({level.answers.Count})，关卡将无法通关！", MessageType.Warning);
        }

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

        // 快捷：将基字本身作为答案（使用基字全部笔画）
        if (!string.IsNullOrEmpty(level.baseCharacter)
            && _characterStrokeCount.TryGetValue(level.baseCharacter, out int baseStrokeCount)
            && baseStrokeCount > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button($"添加基字『{level.baseCharacter}』为答案", GUILayout.Width(220)))
            {
                AddBaseCharacterAsAnswer(level);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.LabelField("（使用基字全部笔画）", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        // 目标字输入（第一行：常用答案 + 已有答案下拉 + 手动输入）
        string[] existingAnswerOptions = GetExistingAnswerOptions(level);
        string[] commonAnswerOptions = GetCommonAnswerOptions();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("常用答案:", GUILayout.Width(60));
        EditorGUI.BeginChangeCheck();
        _selectedCommonAnswerIndex = EditorGUILayout.Popup(_selectedCommonAnswerIndex, commonAnswerOptions, GUILayout.Width(110));
        if (EditorGUI.EndChangeCheck() && _selectedCommonAnswerIndex > 0)
        {
            _newAnswerCharacter = commonAnswerOptions[_selectedCommonAnswerIndex];
        }
        GUILayout.Label("已有:", GUILayout.Width(35));
        EditorGUI.BeginChangeCheck();
        _selectedExistingAnswerIndex = EditorGUILayout.Popup(_selectedExistingAnswerIndex, existingAnswerOptions, GUILayout.Width(120));
        if (EditorGUI.EndChangeCheck() && _selectedExistingAnswerIndex > 0)
        {
            _newAnswerCharacter = existingAnswerOptions[_selectedExistingAnswerIndex];
        }
        GUILayout.Label("或输入:", GUILayout.Width(50));
        _newAnswerCharacter = EditorGUILayout.TextField(_newAnswerCharacter, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        // 笔画索引输入（第二行）
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("笔画索引:", GUILayout.Width(60));
        string strokeInput = EditorGUILayout.TextField(string.Join(",", _newStrokeIndices), GUILayout.ExpandWidth(true));

        if (GUILayout.Button("+ 添加", GUILayout.Width(60)))
        {
            if (AddStrokeSet(level))
            {
                strokeInput = "";
            }
        }
        EditorGUILayout.EndHorizontal();

        ParseStrokeInput(strokeInput);

        // 笔画索引按钮选择（布局同高亮区）：点击即选中/取消，选中按钮高亮并同步场景高亮；
        // 与上方文本框共用 _newStrokeIndices，点击「+ 添加」后自动清空。
        if (!string.IsNullOrEmpty(level.baseCharacter)
            && _characterStrokeCount.TryGetValue(level.baseCharacter, out int selectStrokeCount)
            && selectStrokeCount > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("点击选择笔画（选中高亮，再次点击取消）", EditorStyles.miniLabel);
            DrawNewStrokeSelectButtons(selectStrokeCount);
        }

        EditorGUILayout.HelpBox("笔画索引用逗号或英文句号分隔（支持中/英文逗号、空格、英文句号）。如目标字已有答案，将追加为新组合；否则新建答案。", MessageType.None);

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
            if (EditorUtility.DisplayDialog("确认删除", $"确认删除关卡 {level.levelName}: {level.baseCharacter}？", "删除", "取消"))
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

        // 替换关卡：与列表中另一关卡交换顺序与名称
        DrawSwapLevelRow();

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 替换关卡行：选择目标关卡后点击「替换关卡」，
    /// 将当前选中关卡与目标关卡在列表中互换位置并互换名称（内容随对象移动）。
    /// </summary>
    private void DrawSwapLevelRow()
    {
        var list = _levelDataAsset.levelDataList;
        if (list == null || list.Count < 2) return;

        // 构造候选（排除当前选中关卡自身），并维护下拉索引到实际列表索引的映射
        var candidateIndices = new List<int>();
        var candidateLabels = new List<string>();
        for (int i = 0; i < list.Count; i++)
        {
            if (i == _selectedLevelIndex) continue;
            var lv = list[i];
            if (lv == null) continue;
            candidateIndices.Add(i);
            candidateLabels.Add(string.IsNullOrEmpty(lv.baseCharacter)
                ? $"{lv.levelName} (未设置)"
                : $"{lv.levelName}: {lv.baseCharacter}");
        }
        if (candidateIndices.Count == 0) return;

        // 修正越界的选中索引（关卡被删/重排后可能失效）
        if (_swapTargetIndex < 0 || _swapTargetIndex >= candidateIndices.Count)
            _swapTargetIndex = 0;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("替换关卡（与目标关卡交换位置与名称）", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        _swapTargetIndex = EditorGUILayout.Popup(_swapTargetIndex, candidateLabels.ToArray());
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        if (GUILayout.Button("替换关卡", GUILayout.Width(100)))
        {
            int targetListIndex = candidateIndices[_swapTargetIndex];
            SwapLevels(_selectedLevelIndex, targetListIndex);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 调换两个关卡的顺序与名称：交换二者在列表中的位置，并互换 levelName。
    /// 结果：原 indexA 处保留原名，内容换成原 indexB 的对象；原 indexB 处保留原名，
    /// 内容换成原 indexA 的对象。选中跟随当前关卡对象移动到的新位置。
    /// </summary>
    private void SwapLevels(int indexA, int indexB)
    {
        if (indexA == indexB) return;
        var list = _levelDataAsset.levelDataList;
        if (indexA < 0 || indexA >= list.Count || indexB < 0 || indexB >= list.Count) return;

        var a = list[indexA];
        var b = list[indexB];

        // 名称互换（留在原位置，与对象的位置交换配合，实现“名称与内容同步调换”）
        (a.levelName, b.levelName) = (b.levelName, a.levelName);

        // 列表位置交换
        list[indexA] = b;
        list[indexB] = a;

        // 选中跟随当前关卡对象移动到的新位置
        _selectedLevelIndex = indexB;
        ResetEditState();
        EditorUtility.SetDirty(_levelDataAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"已交换关卡顺序与名称：{a.levelName} ↔ {b.levelName}");
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
            _newAnswerCharacter = EditorGUILayout.TextField("新字符:", _newAnswerCharacter, GUILayout.Width(200));
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

    private static readonly string[] _commonAnswerChars = { "一", "二", "三", "十", "口" , "人", "八", "丨"};

    private string[] GetCommonAnswerOptions()
    {
        var options = new List<string> { "-- 常用答案 --" };
        options.AddRange(_commonAnswerChars);
        return options.ToArray();
    }

    private string[] GetExistingAnswerOptions(TextLevelData level)
    {
        var options = new List<string> { "-- 输入新字 --" };
        if (level != null && level.answers != null)
        {
            foreach (var ans in level.answers)
            {
                if (!string.IsNullOrEmpty(ans.answerCharacter))
                    options.Add(ans.answerCharacter);
            }
        }
        return options.ToArray();
    }

    private void ParseStrokeInput(string input)
    {
        _newStrokeIndices.Clear();
        if (string.IsNullOrWhiteSpace(input)) return;

        string[] parts = input.Split(new[] { ',', '，', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (int.TryParse(part.Trim(), out int index) && index >= 0)
                _newStrokeIndices.Add(index);
        }
    }

    // ==================== 关卡命名 / 排序 / 重名校验 ====================

    /// <summary>Level_ 名称前缀</summary>
    private const string LevelNamePrefix = "Level_";

    /// <summary>
    /// 解析关卡名称序号：Level_29 -> 29；非 Level_ 数字格式返回 -1。
    /// </summary>
    private int ParseLevelIndex(string levelName)
    {
        if (string.IsNullOrEmpty(levelName) || !levelName.StartsWith(LevelNamePrefix))
            return -1;
        string numPart = levelName.Substring(LevelNamePrefix.Length);
        if (int.TryParse(numPart, out int idx) && idx >= 0)
            return idx;
        return -1;
    }

    /// <summary>
    /// 生成下一个关卡名称：取现有 Level_N 中的最大 N +1；无合法项时返回 Level_1。
    /// </summary>
    private string GenerateNextLevelName()
    {
        int maxIndex = 0;
        if (_levelDataAsset != null && _levelDataAsset.levelDataList != null)
        {
            foreach (var lv in _levelDataAsset.levelDataList)
            {
                if (lv == null) continue;
                int idx = ParseLevelIndex(lv.levelName);
                if (idx > maxIndex) maxIndex = idx;
            }
        }
        return LevelNamePrefix + (maxIndex + 1);
    }

    /// <summary>
    /// 校验关卡名称是否重复（排除当前正在编辑的关卡自身）。
    /// </summary>
    private bool IsLevelNameDuplicated(string name, int excludeIndex)
    {
        if (string.IsNullOrEmpty(name) || _levelDataAsset == null || _levelDataAsset.levelDataList == null)
            return false;
        for (int i = 0; i < _levelDataAsset.levelDataList.Count; i++)
        {
            if (i == excludeIndex) continue;
            var lv = _levelDataAsset.levelDataList[i];
            if (lv != null && lv.levelName == name)
                return true;
        }
        return false;
    }

    private static readonly Dictionary<string, string> _customTones = new Dictionary<string, string>()
    {
        { "丨", "gǔn" },
    };


    /// <summary>
    /// 刷新音调数据
    /// </summary>
    private void RefreshTones()
    {
        _levelDataAsset.characterToTone.Clear();
        HashSet<string> answerHashSet = new HashSet<string>();
        foreach (var levelData in _levelDataAsset.levelDataList)
        {
            foreach (var answer in levelData.answers)
            {
                if (answer == null || string.IsNullOrEmpty(answer.answerCharacter) || answerHashSet.Contains(answer.answerCharacter))
                {
                    continue;
                }

                if (!_customTones.TryGetValue(answer.answerCharacter , out var tone))
                {
                    tone = Pinyin4Net.GetPinyin(answer.answerCharacter, PinyinFormat.WITH_TONE_MARK);
                }
                _levelDataAsset.characterToTone.Add(new TextToneData(answer.answerCharacter , tone));
                answerHashSet.Add(answer.answerCharacter);
            }
        }
        EditorUtility.SetDirty(_levelDataAsset);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 按关卡名称中的序号升序重排（Level_1, Level_2, ... Level_29, Level_100）；
    /// 解析不出序号的项按字符串序排在最后，保持相对稳定（使用稳定排序避免乱序）。
    /// </summary>
    private void SortLevels()
    {
        if (_levelDataAsset == null || _levelDataAsset.levelDataList == null)
            return;

        // 记录当前选中的关卡，排序后恢复选中
        TextLevelData selected = (_selectedLevelIndex >= 0 && _selectedLevelIndex < _levelDataAsset.levelDataList.Count)
            ? _levelDataAsset.levelDataList[_selectedLevelIndex]
            : null;

        // OrderBy 稳定排序：先按是否可解析序号分组，再按序号 / 名称排序
        var sorted = _levelDataAsset.levelDataList
            .Select(lv => new { lv, idx = lv != null ? ParseLevelIndex(lv.levelName) : -1 })
            .OrderBy(x => x.idx >= 0 ? 0 : 1)   // 可解析序号的排前
            .ThenBy(x => x.idx)                 // 按序号升序
            .ThenBy(x => x.lv != null ? x.lv.levelName : "") // 同序号或无序号按名称兜底
            .Select(x => x.lv)
            .ToList();

        _levelDataAsset.levelDataList = sorted;

        // 恢复选中
        if (selected != null)
            _selectedLevelIndex = _levelDataAsset.levelDataList.IndexOf(selected);

        EditorUtility.SetDirty(_levelDataAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"关卡已重排，共 {_levelDataAsset.levelDataList.Count} 个");
    }

    private bool AddStrokeSet(TextLevelData level)
    {
        if (string.IsNullOrEmpty(_newAnswerCharacter))
        {
            EditorUtility.DisplayDialog("提示", "请输入目标字字符", "确定");
            return false;
        }
        if (_newStrokeIndices.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请输入至少一个笔画索引", "确定");
            return false;
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
        _selectedExistingAnswerIndex = 0;
        _selectedCommonAnswerIndex = 0;
        // 添加后清除选中与场景高亮（与按钮选择区的选中态联动）
        ResetStrokeHighlight();
        EditorUtility.SetDirty(_levelDataAsset);
        return true;
    }

    /// <summary>
    /// 将当前关卡的基字本身作为一个答案添加：目标字 = 基字，
    /// 笔画组合为基字的全部笔画索引 [0 .. N-1]。
    /// 若该基字已是答案，则校验/补齐「全部笔画」这一组合，避免重复。
    /// </summary>
    private void AddBaseCharacterAsAnswer(TextLevelData level)
    {
        if (level == null || string.IsNullOrEmpty(level.baseCharacter))
        {
            EditorUtility.DisplayDialog("提示", "当前关卡未设置基字", "确定");
            return;
        }

        if (!_characterStrokeCount.TryGetValue(level.baseCharacter, out int strokeCount) || strokeCount <= 0)
        {
            EditorUtility.DisplayDialog("提示", $"未获取到基字『{level.baseCharacter}』的笔画数，请先刷新数据。", "确定");
            return;
        }

        // 基字答案组合 = 全部笔画索引
        var fullIndices = new List<int>();
        for (int i = 0; i < strokeCount; i++)
            fullIndices.Add(i);

        LevelAnswer existing = level.answers.Find(a => a.answerCharacter == level.baseCharacter);
        if (existing != null)
        {
            // 已存在基字答案：检查是否已有「全部笔画」组合，避免重复添加
            bool hasFullSet = existing.strokeSets.Exists(set =>
                set.strokeIndices.Count == fullIndices.Count
                && !set.strokeIndices.Except(fullIndices).Any());

            if (hasFullSet)
            {
                EditorUtility.DisplayDialog("提示",
                    $"基字『{level.baseCharacter}』已作为答案存在，且已包含全部笔画的组合，无需重复添加。",
                    "确定");
                return;
            }

            existing.strokeSets.Add(new StrokeSet { strokeIndices = fullIndices });
            EditorUtility.SetDirty(_levelDataAsset);
            Debug.Log($"已为基字『{level.baseCharacter}』补充『全部笔画』组合");
            return;
        }

        // 新建基字答案
        level.answers.Add(new LevelAnswer
        {
            answerCharacter = level.baseCharacter,
            strokeSets = new List<StrokeSet>
            {
                new StrokeSet { strokeIndices = fullIndices }
            }
        });
        EditorUtility.SetDirty(_levelDataAsset);
        Debug.Log($"已将基字『{level.baseCharacter}』添加为答案（全部 {strokeCount} 笔画）");
    }

    /// <summary>
    /// 添加笔画组合区的笔画索引按钮（布局同 DrawStrokeHighlightButtons）：
    /// 点击即选中/取消（写入 _newStrokeIndices），选中按钮高亮，并同步场景高亮。
    /// </summary>
    private void DrawNewStrokeSelectButtons(int strokeCount)
    {
        // 每行最多显示按钮数
        const int maxPerRow = 8;
        for (int rowStart = 0; rowStart < strokeCount; rowStart += maxPerRow)
        {
            EditorGUILayout.BeginHorizontal();
            int rowEnd = Math.Min(rowStart + maxPerRow, strokeCount);
            for (int i = rowStart; i < rowEnd; i++)
            {
                bool isSelected = _newStrokeIndices.Contains(i);
                GUI.backgroundColor = isSelected ? Color.yellow : Color.white;
                if (GUILayout.Button(i.ToString(), GUILayout.Width(35), GUILayout.Height(25)))
                {
                    if (isSelected)
                        _newStrokeIndices.Remove(i);
                    else
                        _newStrokeIndices.Add(i);
                    // 同步场景高亮：选中的笔画在场景中变黄，与高亮区视觉一致
                    _highlightedStrokeIndices = new List<int>(_newStrokeIndices);
                    ApplyHighlightToScene();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }
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
        _selectedExistingAnswerIndex = 0;
        _selectedCommonAnswerIndex = 0;
        ResetStrokeHighlight();
    }

    private async void RenderBaseCharacterInScene(string ch)
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

        // 如果当前不是 LevelEditScene，自动打开该场景
        const string levelEditScenePath = "Assets/Scenes/LevelEditScene.unity";
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != levelEditScenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(levelEditScenePath);
            }
            else
            {
                return;
            }
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

        // 编辑器预览不经过 loading 预加载，这里先确保材质模板就绪（静态缓存，幂等）
        await DrawCharacter.PreloadStrokeMaterialAsync();
        await drawer.DrawAsync(gd, showStrokeIndices: true);

        // 标记场景已修改
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"已在场景中渲染基字 '{ch}'");
    }
}
#endif
