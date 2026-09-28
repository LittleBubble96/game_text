using System;
using System.Collections.Generic;
using hyjiacan.py4n;
using UnityEngine;

namespace GameLogic.Data
{
    public class TextGraphicDataScriptableObject : ScriptableObject
    {
        [SerializeField]
        public List<TextGraphicData> TextGraphicDataList;
        // 索引只保存地址和字符，不直接引用分片，避免加载索引时连带加载全部字形。
        public List<GraphicChunkEntry> GraphicChunks = new List<GraphicChunkEntry>();
        [SerializeField] private float _pixelScale = 0.002f; // 缩放因子，将 0~1000 坐标缩小到合适大小
        [SerializeField] private int _curveSegments = 10; // 贝塞尔曲线采样点数

#if UNITY_EDITOR
        private const string AssetPath = "Assets/AssetRaw/Configs/LevelConfigs/TextGraphicDataScriptableObject.asset";
        private const string AssetDir = "Assets/AssetRaw/Configs/LevelConfigs";
        private const string TextResPath = "Assets/AssetArt/ConfigData/graphics.txt";

        [NonSerialized] private Dictionary<string, TextGraphicConfigData> _configs;
        [NonSerialized] private Dictionary<string, TextGraphicData> _dataTexts;

        [UnityEditor.MenuItem("Assets/Create/TextGraphicDataScriptableObject")]
        public static void CreateAsset()
        {
            EnsureDirectoryExists();
            TextGraphicDataScriptableObject asset = CreateInstance<TextGraphicDataScriptableObject>();
            UnityEditor.AssetDatabase.CreateAsset(asset, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.Selection.activeObject = asset;
            Debug.Log($"TextGraphicDataScriptableObject 已创建: {AssetPath}");
        }

        public List<TextGraphicData> GetAllGraphicDataEditor()
        {
            var result = new List<TextGraphicData>();
            if (TextGraphicDataList != null) result.AddRange(TextGraphicDataList);
            foreach (var entry in GraphicChunks)
            {
                var chunk = UnityEditor.AssetDatabase.LoadAssetAtPath<TextGraphicDataScriptableObject>(ChunkPath(entry.resourceName));
                if (chunk == null) throw new InvalidOperationException($"找不到字形分片: {entry.resourceName}");
                if (chunk.TextGraphicDataList != null) result.AddRange(chunk.TextGraphicDataList);
            }
            return result;
        }

        private static string ChunkPath(string name) => $"{AssetDir}/Graphics/{name}/{name}.asset";

        private void StoreGraphicEditor(TextGraphicData data)
        {
            var target = this;
            GraphicChunkEntry entry = null;
            if (GraphicChunks.Count > 0)
            {
                entry = GraphicChunks.Find(x => x.characters.Contains(data.character));
                if (entry == null)
                {
                    // 尚未配置关卡的字也单独存放，不让主索引重新膨胀。
                    const string extra = "TextGraphics_Unassigned";
                    entry = GraphicChunks.Find(x => x.resourceName == extra);
                    if (entry == null)
                    {
                        entry = new GraphicChunkEntry { resourceName = extra, characters = "" };
                        GraphicChunks.Add(entry);
                    }
                }
                string path = ChunkPath(entry.resourceName);
                target = UnityEditor.AssetDatabase.LoadAssetAtPath<TextGraphicDataScriptableObject>(path);
                if (target == null)
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    UnityEditor.AssetDatabase.Refresh();
                    target = CreateInstance<TextGraphicDataScriptableObject>();
                    target._pixelScale = _pixelScale;
                    target._curveSegments = _curveSegments;
                    UnityEditor.AssetDatabase.CreateAsset(target, path);
                }
                if (!entry.characters.Contains(data.character)) entry.characters += data.character;
            }
            if (target.TextGraphicDataList == null) target.TextGraphicDataList = new List<TextGraphicData>();
            int index = target.TextGraphicDataList.FindIndex(x => x.character == data.character);
            if (index < 0) target.TextGraphicDataList.Add(data);
            else target.TextGraphicDataList[index] = data;
            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [UnityEditor.MenuItem("Tools/关卡/按25关重建字形分片")]
        public static void RebuildGraphicChunksEditor()
        {
            var manifest = UnityEditor.AssetDatabase.LoadAssetAtPath<TextGraphicDataScriptableObject>(AssetPath);
            var levels = UnityEditor.AssetDatabase.LoadAssetAtPath<TextLevelDataScriptableObject>($"{AssetDir}/TextLevelDataScriptableObject.asset");
            if (manifest == null || levels == null) throw new InvalidOperationException("缺少字形或关卡配置");
            var graphics = manifest.GetAllGraphicDataEditor();
            var owners = new Dictionary<string, int>();
            foreach (var level in levels.levelDataList)
            {
                if (!int.TryParse(level.levelName.Replace("Level_", ""), out int id) || id < 1) continue;
                if (!owners.TryGetValue(level.baseCharacter, out int previous) || id < previous)
                    owners[level.baseCharacter] = id;
            }
            var groups = new SortedDictionary<string, List<TextGraphicData>>();
            foreach (var graphic in graphics)
            {
                string name = "TextGraphics_Unassigned";
                if (owners.TryGetValue(graphic.character, out int id))
                {
                    int first = (id - 1) / 25 * 25 + 1;
                    name = $"TextGraphics_{first:D3}_{first + 24:D3}";
                }
                if (!groups.TryGetValue(name, out var items)) groups[name] = items = new List<TextGraphicData>();
                items.Add(graphic);
            }
            var oldEntries = new List<GraphicChunkEntry>(manifest.GraphicChunks);
            var newEntries = new List<GraphicChunkEntry>();
            foreach (var group in groups)
            {
                string path = ChunkPath(group.Key);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                UnityEditor.AssetDatabase.Refresh();
                var chunk = UnityEditor.AssetDatabase.LoadAssetAtPath<TextGraphicDataScriptableObject>(path);
                if (chunk == null)
                {
                    chunk = CreateInstance<TextGraphicDataScriptableObject>();
                    UnityEditor.AssetDatabase.CreateAsset(chunk, path);
                }
                chunk.TextGraphicDataList = group.Value;
                chunk._pixelScale = manifest._pixelScale;
                chunk._curveSegments = manifest._curveSegments;
                string characters = "";
                foreach (var graphic in group.Value) characters += graphic.character;
                newEntries.Add(new GraphicChunkEntry { resourceName = group.Key, characters = characters });
                UnityEditor.EditorUtility.SetDirty(chunk);
            }
            manifest.TextGraphicDataList = new List<TextGraphicData>();
            manifest.GraphicChunks = newEntries;
            UnityEditor.EditorUtility.SetDirty(manifest);
            UnityEditor.AssetDatabase.SaveAssets();
            foreach (var old in oldEntries)
                if (!groups.ContainsKey(old.resourceName)) UnityEditor.AssetDatabase.DeleteAsset(ChunkPath(old.resourceName));
            Debug.Log($"字形分片完成: {graphics.Count} 字 / {groups.Count} 个分片");
        }

        public bool CheckHasCharacter(string input)
        {
            foreach (var t in GetAllGraphicDataEditor())
            {
                if (t != null && t.character == input)
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnsureDirectoryExists()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(AssetDir))
            {
                // 逐级检查父目录
                string parent = "Assets/AssetRaw/Configs";
                if (!UnityEditor.AssetDatabase.IsValidFolder(parent))
                {
                    parent = "Assets/AssetRaw";
                    if (!UnityEditor.AssetDatabase.IsValidFolder(parent))
                    {
                        UnityEditor.AssetDatabase.CreateFolder("Assets", "AssetRaw");
                    }
                    UnityEditor.AssetDatabase.CreateFolder("Assets/AssetRaw", "Configs");
                }
                UnityEditor.AssetDatabase.CreateFolder("Assets/AssetRaw/Configs", "LevelConfigs");
                Debug.Log($"已创建目录: {AssetDir}");
            }
        }

        public void ReGenerate()
        {
            var allGraphics = GetAllGraphicDataEditor();
            if (allGraphics.Count == 0) return;
            var chars = new List<string>(allGraphics.Count);
            foreach (var t in allGraphics)
            {
                if (t != null)
                    chars.Add(t.character);
            }

            foreach (var ch in chars)
            {
                Generate(ch);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        public string Generate(string input)
        {
            if (input == null || input.Length != 1)
            {
                return "Input is not a single character.";
            }

            if (_configs == null || _dataTexts == null)
            {
                _configs = new Dictionary<string, TextGraphicConfigData>();
                _dataTexts = new Dictionary<string, TextGraphicData>();
                LoadAndParseGraphics(_configs, _dataTexts);
            }

            if (!_dataTexts.TryGetValue(input, out var graphicData))
            {
                return $"Character '{input}' not found in graphics data.";
            }

            StoreGraphicEditor(graphicData);
            //保存
            UnityEditor.AssetDatabase.SaveAssets();
            return string.Empty;
        }

        private void LoadAndParseGraphics(Dictionary<string, TextGraphicConfigData> configs,
            Dictionary<string, TextGraphicData> datas)
        {
            TextAsset textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(TextResPath);
            if (textAsset == null)
            {
                Debug.LogError($"无法加载文本资源：{TextResPath}");
                return;
            }

            string[] lines = textAsset.text.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                TextGraphicConfigData configData = JsonUtility.FromJson<TextGraphicConfigData>(line);
                if (configData == null || string.IsNullOrEmpty(configData.character))
                {
                    Debug.LogWarning($"解析失败，跳过行：{line}");
                    continue;
                }

                configs[configData.character] = configData;

                TextGraphicData graphicData = ConvertToTextGraphicData(configData);
                datas[configData.character] = graphicData;
            }

            Debug.Log($"成功加载 {datas.Count} 个汉字的笔画数据");
        }

        /// <summary>
        /// 将原始配置中的 SVG 路径字符串转为 Vector2 点集
        /// </summary>
        private TextGraphicData ConvertToTextGraphicData(TextGraphicConfigData config)
        {
            TextGraphicData data = new TextGraphicData();
            data.character = config.character;
            data.strokes = new List<TextDrawPoints>();

            for (int i = 0; i < config.strokes.Count; i++)
            {
                string svgPath = config.strokes[i];
                List<Vector2> points = ParseSvgStrokeToPoints(svgPath, _pixelScale, _curveSegments);
                data.strokes.Add(new TextDrawPoints(points));
            }

            return data;
        }

        /// <summary>
        /// 解析单个笔画的 SVG 路径（支持 M, Q, Z ,L(直线)，C(三次贝塞尔)命令）
        /// 返回不闭合的折线点集（按绘制顺序）
        /// </summary>
        private List<Vector2> ParseSvgStrokeToPoints(string svgPath, float scale, int segments)
        {
            List<Vector2> points = new List<Vector2>();
            string[] tokens = svgPath.Split(new char[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            Vector2 currentPoint = Vector2.zero;
            int idx = 0;
            int length = tokens.Length;

            while (idx < length)
            {
                string cmd = tokens[idx];
                idx++;

                switch (cmd)
                {
                    case "M":
                        if (idx + 1 >= length) break;
                        float mx = float.Parse(tokens[idx]);
                        idx++;
                        float my = float.Parse(tokens[idx]);
                        idx++;
                        currentPoint = new Vector2(mx, my);
                        points.Add(currentPoint * scale);
                        break;

                    case "L":
                        if (idx + 1 >= length) break;
                        float lx = float.Parse(tokens[idx]);
                        idx++;
                        float ly = float.Parse(tokens[idx]);
                        idx++;
                        Vector2 lineEnd = new Vector2(lx, ly);
                        points.Add(lineEnd * scale);
                        currentPoint = lineEnd;
                        break;

                    case "Q":
                        if (idx + 3 >= length) break;
                        float cx = float.Parse(tokens[idx]);
                        idx++;
                        float cy = float.Parse(tokens[idx]);
                        idx++;
                        float ex = float.Parse(tokens[idx]);
                        idx++;
                        float ey = float.Parse(tokens[idx]);
                        idx++;
                        Vector2 control = new Vector2(cx, cy);
                        Vector2 end = new Vector2(ex, ey);
                        for (int s = 1; s <= segments; s++)
                        {
                            float t = s / (float)segments;
                            Vector2 point = BezierQuadratic(currentPoint, control, end, t);
                            points.Add(point * scale);
                        }
                        currentPoint = end;
                        break;

                    case "C":
                        if (idx + 5 >= length) break;
                        float c1x = float.Parse(tokens[idx]);
                        idx++;
                        float c1y = float.Parse(tokens[idx]);
                        idx++;
                        float c2x = float.Parse(tokens[idx]);
                        idx++;
                        float c2y = float.Parse(tokens[idx]);
                        idx++;
                        float endX = float.Parse(tokens[idx]);
                        idx++;
                        float endY = float.Parse(tokens[idx]);
                        idx++;
                        Vector2 control1 = new Vector2(c1x, c1y);
                        Vector2 control2 = new Vector2(c2x, c2y);
                        Vector2 endC = new Vector2(endX, endY);
                        for (int s = 1; s <= segments; s++)
                        {
                            float t = s / (float)segments;
                            Vector2 point = BezierCubic(currentPoint, control1, control2, endC, t);
                            points.Add(point * scale);
                        }
                        currentPoint = endC;
                        break;

                    case "Z":
                        break;

                    default:
                        Debug.LogWarning($"未知 SVG 命令: {cmd}");
                        break;
                }
            }

            return points;
        }

        private Vector2 BezierCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        private Vector2 BezierQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1 - t;
            return u * u * p0 + 2 * u * t * p1 + t * t * p2;
        }
#endif

    }
    
    [Serializable]
    public class GraphicChunkEntry
    {
        public string resourceName;
        public string characters;
    }

    // 以下类定义应与 JSON 结构匹配
    [System.Serializable]
    public class TextGraphicConfigData
    {
        public string character;
        public List<string> strokes;
        public List<List<List<int>>> medians; // 可选，不使用
    }

    [System.Serializable]
    public class TextGraphicData
    {
        public string character;

        // 笔画索引 -> 点列表（不闭合，按绘制顺序）
        public List<TextDrawPoints> strokes;
    }

    [System.Serializable]
    public class TextDrawPoints
    {
        public TextDrawPoints(List<Vector2> points)
        {
            this.points = points;
        }

        public List<Vector2> points; // 按绘制顺序的所有点（可选，按需生成）
    }
}