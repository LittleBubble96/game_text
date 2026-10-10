using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameLogic.Data;
using GameLogic.GamePlay.CorePlay;
using UnityEngine;

namespace UnityEngine
{
    public class ScriptableObject { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class SerializeField : Attribute { }
    public interface ISerializationCallbackReceiver { void OnBeforeSerialize(); void OnAfterDeserialize(); }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    public static class Mathf
    {
        public const float Epsilon = float.Epsilon;
        public static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(0.000001f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8);
    }
}
namespace UnityEngine.Serialization
{
    public class FormerlySerializedAsAttribute : Attribute { public FormerlySerializedAsAttribute(string name) { } }
}
namespace GameLogic.View { }

internal static class Program
{
    private static readonly FieldInfo Packed = typeof(TextDrawPoints).GetField("quantizedPoints", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo Floats = typeof(TextDrawPoints).GetField("floatPoints", BindingFlags.NonPublic | BindingFlags.Instance);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static List<Vector2> ParsePoints(string text) => Regex.Matches(text, @"\{x: ([^,]+), y: ([^}]+)\}")
        .Select(m => new Vector2(float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))).ToList();

    public static void Main(string[] args)
    {
        string root = Path.Combine(Path.GetFullPath(args[0]), "Assets/AssetRaw/Configs/LevelConfigs/Graphics");
        var outputs = new Dictionary<string, string>();
        var strokeCounts = new Dictionary<string, int>();
        int strokes = 0, points = 0, fallback = 0;
        double maxError = 0;
        foreach (var file in Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            // This migration deliberately accepts only legacy blocks. Re-running leaves converted data unchanged.
            string output = Regex.Replace(source, @"(?m)^    - points:\r?\n(?:      - \{x: [^\r\n]+\}\r?\n)+", match =>
            {
                var original = ParsePoints(match.Value);
                var data = new TextDrawPoints(original);
                var decoded = data.points;
                Check(decoded.Count == original.Count, "Point count changed");
                for (int i = 0; i < original.Count; i++)
                {
                    double error = Math.Max(Math.Abs(original[i].x - decoded[i].x), Math.Abs(original[i].y - decoded[i].y));
                    Check(error <= 0.5 / TextDrawPoints.CoordinateScale, "Coordinate error too large");
                    maxError = Math.Max(error, maxError);
                }
                Check(new Triangulator(original.ToArray()).Triangulate().Length ==
                      new Triangulator(decoded.ToArray()).Triangulate().Length, "Triangle count changed");
                strokes++; points += original.Count;
                var packed = (List<QuantizedPoint>)Packed.GetValue(data);
                if (packed == null)
                {
                    fallback++;
                    return match.Value.Replace("- points:", "- floatPoints:") + "      quantizedPoints: []\n";
                }
                Check(!data.Quantize(), "Migration must be idempotent");
                Check(ReferenceEquals(decoded, data.points), "Decoded points not cached");
                data.OnAfterDeserialize();
                Check(!ReferenceEquals(decoded, data.points), "Deserialize did not invalidate cache");
                return "    - floatPoints: []\n      quantizedPoints:\n" +
                    string.Concat(packed.Select(p => $"      - x: {p.x}\n        y: {p.y}\n"));
            });
            outputs.Add(file, output);
            foreach (string graphic in Regex.Split(output, @"(?m)^  - character: ").Skip(1))
            {
                string name = graphic.Split('\n')[0].Trim();
                string character = name.StartsWith('"') ? JsonSerializer.Deserialize<string>(name) : name;
                strokeCounts.Add(character, Regex.Matches(graphic, @"(?m)^    - floatPoints:").Count);
            }
            // Validate serialized field pairing and rehydrate the actual production type from written values.
            foreach (Match block in Regex.Matches(output, @"(?ms)^    - floatPoints:.*?(?=^    - floatPoints:|^  - character:|^  GraphicChunks:|\z)"))
            {
                string text = block.Value;
                var data = new TextDrawPoints(new List<Vector2>());
                Floats.SetValue(data, ParsePoints(text));
                var packed = Regex.Matches(text, @"(?m)^      - x: (-?\d+)\r?\n        y: (-?\d+)")
                    .Select(m => new QuantizedPoint { x = short.Parse(m.Groups[1].Value), y = short.Parse(m.Groups[2].Value) }).ToList();
                Packed.SetValue(data, packed);
                data.OnAfterDeserialize();
                Check(data.points != null && data.points.Count > 0, "Serialized stroke cannot be restored");
            }
        }
        int answerIndices = 0;
        string levels = File.ReadAllText(Path.Combine(root, "../TextLevelDataScriptableObject.asset"));
        foreach (string level in Regex.Split(levels, @"(?m)^  - levelName: ").Skip(1))
        {
            string character = JsonSerializer.Deserialize<string>(Regex.Match(level, @"baseCharacter: (""[^""]+"")").Groups[1].Value);
            foreach (Match set in Regex.Matches(level, @"strokeIndices: ([0-9a-fA-F]+)"))
            {
                byte[] indices = Convert.FromHexString(set.Groups[1].Value);
                for (int i = 0; i < indices.Length; i += 4)
                {
                    int index = BitConverter.ToInt32(indices, i);
                    Check(index >= 0 && index < strokeCounts[character], "Answer/save stroke index out of range");
                    answerIndices++;
                }
            }
        }
        // Existing saves contain no geometry. Exercise production save model/restorer with an old snapshot.
        const string json = """
            {"currentLevelId":1,"foundAnswerIndices":[0],"cachedLevelData":{"levelName":"Level_1","baseCharacter":"干","answers":[{"answerCharacter":"一","strokeSets":[{"strokeIndices":[0]}]},{"answerCharacter":"十","strokeSets":[{"strokeIndices":[1,2]}]}]}}
            """;
        var options = new JsonSerializerOptions { IncludeFields = true };
        var restore = new CorePlayRestore();
        restore.LoadFromData(JsonSerializer.Deserialize<CorePlaySaveData>(json, options));
        var snapshot = restore.GetCachedLevelData();
        Check(snapshot.baseCharacter == "干" && restore.GetFoundAnswers().SequenceEqual(new[] { 0 }), "Old save restore failed");
        restore.SaveCurrentProgress(1, new List<int> { 0, 1 }, null);
        Check(ReferenceEquals(snapshot, restore.GetCachedLevelData()), "Lost saved snapshot");
        var saved = JsonSerializer.Deserialize<CorePlaySaveData>(JsonSerializer.Serialize(restore.SaveData, options), options);
        Check(saved.foundAnswerIndices.SequenceEqual(new[] { 0, 1 }) && saved.cachedLevelData.answers[1].strokeSets[0].strokeIndices.SequenceEqual(new[] { 1, 2 }), "Saved indices changed");
        Check(Packed.GetValue(new TextDrawPoints(new List<Vector2> { new Vector2(8f, 0) })) == null, "Out-of-range coordinate must stay float");
        Console.WriteLine($"PASS: chunks={outputs.Count}, characters={strokeCounts.Count}, migratedStrokes={strokes}, points={points}, floatFallback={fallback}, maxError={maxError}, answerIndices={answerIndices}; old-save restore/continue/re-save PASS");
        if (args.Contains("--migrate")) foreach (var pair in outputs) File.WriteAllText(pair.Key, pair.Value);
    }
}
