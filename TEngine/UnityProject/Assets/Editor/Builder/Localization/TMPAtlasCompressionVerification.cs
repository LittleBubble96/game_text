using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Builder;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

internal static class TMPAtlasCompressionVerification
{
    private const string Root = "Assets/__TMPAtlasCompressionValidation";
    private const string Report = "Temp/TMPAtlasCompressionValidation/result.txt";
    private static readonly StringBuilder Results = new StringBuilder();

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (File.Exists("Temp/TMPAtlasCompressionValidation/run.request") && !File.Exists(Report))
            EditorApplication.delayCall += Run;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Results.AppendLine("PASS " + message);
    }

    private static TMP_FontAsset Create(string text, bool isStatic = true, int width = 256)
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/AssetArt/Font/TaiWanQuanZiKuZhengKaiTi-2.ttf");
        var font = TMP_FontAsset.CreateFontAsset(source, 60, 6, GlyphRenderMode.SDF8, width, 256,
            AtlasPopulationMode.Dynamic, false);
        Check(font.TryAddCharacters(text), "Bake " + text);
        if (isStatic) font.atlasPopulationMode = AtlasPopulationMode.Static;
        return font;
    }

    private static void Save(TMP_FontAsset font, string path)
    {
        typeof(LanguageGenerateEditor).GetMethod("SaveFontAsset", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { font, path, "Verification SDF" });
    }

    private static void CheckPng(string path, Color32[] expected)
    {
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        try
        {
            Check(image.LoadImage(File.ReadAllBytes(path)), "PNG decode");
            var actual = image.GetPixels32();
            Check(actual.Length == expected.Length && actual.Select(p => p.a).SequenceEqual(expected.Select(p => p.a)),
                "PNG preserves all original Alpha bytes");
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }

    private static void CheckSettings(Texture2D texture)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        Check(!importer.isReadable && !importer.mipmapEnabled && !importer.sRGBTexture && !importer.alphaIsTransparency,
            "Static import flags");
        Check(importer.alphaSource == TextureImporterAlphaSource.FromInput, "Alpha source retained");
        Check(importer.GetDefaultPlatformTextureSettings().format == TextureImporterFormat.Alpha8, "Default Alpha8");
        foreach (var platform in new[] { "Android", "iPhone", "WebGL" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            Check(settings.overridden && settings.format == TextureImporterFormat.ASTC_6x6, platform + " ASTC 6x6");
        }
    }

    private static void Run()
    {
        try
        {
            Check(!EditorApplication.isPlaying, "Editor not playing");
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "__TMPAtlasCompressionValidation");
            const string path = Root + "/Verification SDF.asset";
            const string pngPath = Root + "/Verification SDF_tex.png";

            // 模拟现有不可读的内嵌图集，并建立主材质、预设及 fallback 引用。
            var existing = Create("AB");
            existing.name = "Verification SDF";
            var originalAtlas = existing.atlasTexture;
            var originalMaterial = existing.material;
            AssetDatabase.CreateAsset(existing, path);
            AssetDatabase.AddObjectToAsset(originalMaterial, existing);
            AssetDatabase.AddObjectToAsset(originalAtlas, existing);
            GenerateTMPUtility.SetTextureReadable(originalAtlas, false);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/AssetRaw/Fonts/TaiWanQuanZiKuZhengKaiTi-2 SDF.asset");
            existing.fallbackFontAssetTable.Add(fallback);
            var preset = new Material(existing.material);
            preset.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.13f);
            AssetDatabase.CreateAsset(preset, Root + "/Preset.mat");
            AssetDatabase.SaveAssets();
            string fontGuid = AssetDatabase.AssetPathToGUID(path);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(originalMaterial, out string materialGuid, out long materialId);

            var generated = Create("ABC");
            var expected = generated.atlasTexture.GetPixels32();
            Save(generated, path);
            var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Check(saved == existing && saved.material == originalMaterial, "Font and main material references preserved");
            Check(saved.atlasTexture == saved.atlasTextures[0] && saved.atlasTexture != originalAtlas, "Atlas cache replaced");
            Check(!AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().Any(), "Old embedded atlas removed");
            Check(saved.material.mainTexture == saved.atlasTexture && preset.mainTexture == saved.atlasTexture,
                "Main material and preset rebound");
            Check(Mathf.Approximately(preset.GetFloat(ShaderUtilities.ID_OutlineWidth), 0.13f), "Preset outline preserved");
            Check(saved.fallbackFontAssetTable.Contains(fallback), "Fallback preserved");
            CheckPng(pngPath, expected);
            CheckSettings(saved.atlasTexture);
            string textureGuid = AssetDatabase.AssetPathToGUID(pngPath);

            // 重复生成并改变尺寸，确认外部 PNG 不被 CopySerialized 破坏。
            generated = Create("ABCDE", true, 512);
            expected = generated.atlasTexture.GetPixels32();
            Save(generated, path);
            saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(saved.material, out string newMaterialGuid, out long newMaterialId);
            Check(fontGuid == AssetDatabase.AssetPathToGUID(path) && materialGuid == newMaterialGuid && materialId == newMaterialId,
                "Font GUID and material local ID stable on regeneration");
            Check(textureGuid == AssetDatabase.AssetPathToGUID(pngPath), "PNG GUID stable on regeneration");
            Check(saved.atlasTexture.width == 512 && preset.mainTexture == saved.atlasTexture
                && preset.GetFloat(ShaderUtilities.ID_TextureWidth) == 512, "Resized atlas and material dimensions updated");
            CheckPng(pngPath, expected);

            generated = Create("A", false);
            Save(generated, path);
            saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Check(saved.atlasPopulationMode == AtlasPopulationMode.Dynamic && saved.sourceFontFile != null
                && saved.atlasTexture.isReadable && AssetDatabase.IsSubAsset(saved.atlasTexture), "Dynamic fallback stays embedded and writable");
            Check(saved.TryAddCharacters("B"), "Dynamic fallback can add a character after save");
            Check(preset.mainTexture == saved.atlasTexture, "Preset rebound when switching back to Dynamic");
            bool rejected = false;
            try { TMPAtlasConverter.ExportAtlases(saved, path, TextureImporterFormat.ASTC_6x6, 4096, 100); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Dynamic compression rejected");

            generated = Create("ABC");
            Save(generated, path);
            saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Check(saved.atlasPopulationMode == AtlasPopulationMode.Static && preset.mainTexture == saved.atlasTexture,
                "Dynamic to Static conversion");

            var multi = Create("A");
            var first = multi.atlasTexture;
            var second = new Texture2D(256, 256, TextureFormat.Alpha8, false);
            second.LoadRawTextureData(Enumerable.Range(0, 256 * 256).Select(i => (byte)(i % 251)).ToArray());
            second.Apply();
            GenerateTMPUtility.SetAtlasTextures(multi, new[] { first, second });
            var multiTextures = TMPAtlasConverter.ExportAtlases(multi, Root + "/Multi SDF.asset", TextureImporterFormat.ASTC_6x6, 4096, 100);
            Check(multiTextures.Length == 2 && AssetDatabase.GetAssetPath(multiTextures[1]).EndsWith("_tex_1.png"), "All atlas pages exported");
            CheckPng(Root + "/Multi SDF_tex_1.png", second.GetPixels32());
            CheckSettings(multiTextures[1]);
            rejected = false;
            try { TMPAtlasConverter.ExportAtlases(multi, Root + "/TooSmall.asset", TextureImporterFormat.ASTC_6x6, 128, 100); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Downsampling rejected");
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(multi.material);
            UnityEngine.Object.DestroyImmediate(multi);
            Results.AppendLine("ALL TESTS PASSED");
        }
        catch (Exception exception)
        {
            Results.AppendLine("FAILED " + exception);
            Debug.LogException(exception);
        }
        File.WriteAllText(Report, Results.ToString());
        Debug.Log("[TMP Atlas verification] " + Results.ToString());
    }
}
