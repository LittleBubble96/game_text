using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameLogic.Data;
using GameLogic.GamePlay.CorePlay;

namespace GameLogic.Data
{
    public class StrokeSet { public List<int> strokeIndices = new(); }
    public class LevelAnswer { public List<StrokeSet> strokeSets = new(); }
}

internal static class LookupTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    // Original gameplay's sorted-sequence comparison, independent of the new index.
    private static List<int> Original(List<LevelAnswer> answers, HashSet<int> selection)
    {
        var sorted = selection.OrderBy(x => x).ToArray();
        return Enumerable.Range(0, answers.Count)
            .Where(i => answers[i].strokeSets.Any(s => s.strokeIndices.OrderBy(x => x).SequenceEqual(sorted)))
            .ToList();
    }

    private static int Resolve(List<int> matches, HashSet<int> found)
    {
        foreach (int index in matches) if (!found.Contains(index)) return index;
        return matches.Count == 0 ? -1 : -2; // wrong / duplicate
    }

    public static void Main(string[] args)
    {
        var raw = JsonSerializer.Deserialize<List<List<List<List<int>>>>>(File.ReadAllText(args[0]));
        // Shared combinations, duplicate indices, bit 63, fallback beyond 64, negatives, unordered input.
        raw.Add(new List<List<List<int>>> {
            new() { new() { 1, 3 }, new() { 3, 1 } },
            new() { new() { 3, 1 } },
            new() { new() { 2, 2 } },
            new() { new() { 0, 63 } },
            new() { new() { 64, 1 } },
            new() { new() { 100, 64, 1 }, new() { -1, 0 } }
        });
        var lookup = new StrokeAnswerLookup();
        var random = new Random(9127);
        int checkedSelections = 0;
        foreach (var level in raw)
        {
            var answers = level.Select(a => new LevelAnswer {
                strokeSets = a.Select(s => new StrokeSet { strokeIndices = s }).ToList()
            }).ToList();
            lookup.Build(answers);
            var selections = level.SelectMany(a => a).Select(s => new HashSet<int>(s)).ToList();
            for (int i = 0; i < 200; i++)
                selections.Add(new HashSet<int>(Enumerable.Range(0, random.Next(1, 12)).Select(_ => random.Next(0, 81))));
            foreach (var selection in selections)
            {
                var expected = Original(answers, selection);
                var actual = lookup.Find(selection) ?? new List<int>();
                Check(actual.SequenceEqual(expected), "New lookup differs from original sorted matching");
                var found = new HashSet<int>();
                foreach (int index in expected)
                {
                    Check(Resolve(actual, found) == Resolve(expected, found), "Shared answer order changed");
                    found.Add(index);
                }
                Check(Resolve(actual, found) == Resolve(expected, found), "Duplicate/wrong resolution changed");
                checkedSelections++;
            }
        }
        lookup.Build(new List<LevelAnswer> { new() { strokeSets = new() { new() { strokeIndices = new() { 1, 3 } } } } });
        var hotSelection = new HashSet<int> { 3, 1 };
        for (int i = 0; i < 10000; i++) lookup.Find(hotSelection);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) lookup.Find(hotSelection);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "Common lookup allocates managed memory");
        lookup.Build(new List<LevelAnswer>());
        Check(lookup.Find(hotSelection) == null, "Switching levels retains stale answers");
        Console.WriteLine($"PASS: {checkedSelections} selections match original behavior; shared/duplicate/>64/reset cases pass; 10000 hot lookups allocate {allocated} bytes.");
    }
}
