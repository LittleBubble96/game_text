using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameLogic.Data
{


    public class TextGraphicDataScriptableObject : ScriptableObject
    {
        //不序列花
        [NonSerialized] private string _textResPath = "Assets/AssetArt/graphics.txt";
        public List<TextGraphicData> TextGraphicDataList;
        [SerializeField] private float _pixelScale = 0.002f; // 缩放因子，将 0~1000 坐标缩小到合适大小
        [SerializeField] private int _curveSegments = 10; // 贝塞尔曲线采样点数

        [NonSerialized] private Dictionary<string, TextGraphicConfigData> _configs;

        [NonSerialized] private Dictionary<string, TextGraphicData> _dataTexts;

        //创建
        [UnityEditor.MenuItem("Assets/Create/TextGraphicDataScriptableObject")]
        public static void CreateAsset()
        {
            TextGraphicDataScriptableObject asset = ScriptableObject.CreateInstance<TextGraphicDataScriptableObject>();
            UnityEditor.AssetDatabase.CreateAsset(asset, "Assets/Resources/Res/TextGraphicDataScriptableObject.asset");
            UnityEditor.AssetDatabase.SaveAssets();
        }

        public void ReGenerate()
        {
            // 防止在循环中被 Generate 修改集合导致 "Collection was modified" 错误
            if (TextGraphicDataList == null || TextGraphicDataList.Count == 0)
                return;

            // 复制需要重生成的字符列表，然后对副本循环，确保安全
            var chars = new List<string>(TextGraphicDataList.Count);
            foreach (var t in TextGraphicDataList)
            {
                if (t != null)
                    chars.Add(t.character);
            }

            foreach (var ch in chars)
            {
                Generate(ch);
            }
        }

        //返回报错
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

            //如果之前不包含 直接加进入
            if (TextGraphicDataList == null)
            {
                TextGraphicDataList = new List<TextGraphicData>();
            }

            int findIndex = TextGraphicDataList.FindIndex(g => g.character == input);
            if (findIndex == -1)
            {
                TextGraphicDataList.Add(graphicData);
            }
            else
            {
                TextGraphicDataList[findIndex] = graphicData;
            }

            return string.Empty;
        }

        private void LoadAndParseGraphics(Dictionary<string, TextGraphicConfigData> configs,
            Dictionary<string, TextGraphicData> datas)
        {
            // 1. 从 Resources 加载文本文件
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(_textResPath);
            if (textAsset == null)
            {
                Debug.LogError($"无法加载文本资源：{_textResPath}");
                return;
            }

            string[] lines = textAsset.text.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 2. 解析单行 JSON
                TextGraphicConfigData configData = JsonUtility.FromJson<TextGraphicConfigData>(line);
                if (configData == null || string.IsNullOrEmpty(configData.character))
                {
                    Debug.LogWarning($"解析失败，跳过行：{line}");
                    continue;
                }

                // 3. 存入原始配置字典
                configs[configData.character] = configData;

                // 4. 将 SVG 笔画转换为点集（不闭合）
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
                    case "M": //移动
                        if (idx + 1 >= length) break;
                        float mx = float.Parse(tokens[idx]);
                        idx++;
                        float my = float.Parse(tokens[idx]);
                        idx++;
                        // currentPoint = new Vector2(mx, 1000 - my);
                        currentPoint = new Vector2(mx, my);
                        points.Add(currentPoint * scale);
                        break;

                    case "L": // 直线
                        if (idx + 1 >= length) break;
                        float lx = float.Parse(tokens[idx]);
                        idx++;
                        float ly = float.Parse(tokens[idx]);
                        idx++;
                        // Vector2 lineEnd = new Vector2(lx, 1000 - ly);
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
                        // Vector2 control = new Vector2(cx, 1000 - cy);
                        // Vector2 end = new Vector2(ex, 1000 - ey);
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

                    case "C": // 三次贝塞尔曲线（可选，增强兼容性）
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
                        // Vector2 control1 = new Vector2(c1x, 1000 - c1y);
                        // Vector2 control2 = new Vector2(c2x, 1000 - c2y);
                        // Vector2 endC = new Vector2(endX, 1000 - endY);
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
                        // 闭合路径：可选，本项目中不添加额外点
                        break;

                    default:
                        Debug.LogWarning($"未知 SVG 命令: {cmd}");
                        break;
                }
            }

            return points;
        }

        // 添加三次贝塞尔辅助函数
        private Vector2 BezierCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        /// <summary>
        /// 二次贝塞尔曲线插值
        /// </summary>
        private Vector2 BezierQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1 - t;
            return u * u * p0 + 2 * u * t * p1 + t * t * p2;
        }

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