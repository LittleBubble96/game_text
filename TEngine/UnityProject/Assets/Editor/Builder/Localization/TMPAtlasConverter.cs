using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Builder
{
    /// <summary>将静态 SDF 图集导出为 PNG，并配置移动端和 WebGL 的纹理压缩。</summary>
    public static class TMPAtlasConverter
    {
        public static Texture2D[] ExportAtlases(TMP_FontAsset fontAsset, string assetPath,
            TextureImporterFormat format, int maxSize, int compressionQuality)
        {
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
                throw new InvalidOperationException("只有完整预烘焙的静态字体可以导出压缩图集。");

            Texture2D[] atlases = fontAsset.atlasTextures;
            // 先检查所有图集，避免导出到一半才发现尺寸或可读性不符合要求。
            foreach (Texture2D atlas in atlases)
            {
                if (atlas == null || atlas.width == 0 || atlas.height == 0 || !atlas.isReadable)
                    throw new InvalidOperationException("请在关闭图集可读性之前导出字体图集。");
                if (Math.Max(atlas.width, atlas.height) > maxSize)
                    throw new InvalidOperationException("压缩图集的最大尺寸不能小于原图，避免重采样破坏 SDF 精度。");
            }

            string textureBasePath = Path.ChangeExtension(assetPath, null);
            var textures = new Texture2D[atlases.Length];
            for (int i = 0; i < atlases.Length; i++)
            {
                string texturePath = i == 0 ? textureBasePath + "_tex.png" : textureBasePath + $"_tex_{i}.png";
                WriteAtlasPng(atlases[i], texturePath);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException($"无法加载字体图集导入器: {texturePath}");

                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false; // Alpha 存储距离数据，不执行透明边缘处理。
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;

                // 其他平台保留单通道，避免 PNG 默认导入成 RGBA32 增加显存。
                TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
                defaults.format = TextureImporterFormat.Alpha8;
                defaults.maxTextureSize = maxSize;
                importer.SetPlatformTextureSettings(defaults);

                foreach (string platform in new[] { "Android", "iPhone", "WebGL" })
                {
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                    settings.name = platform;
                    settings.overridden = true;
                    settings.format = format;
                    settings.maxTextureSize = maxSize;
                    settings.compressionQuality = compressionQuality;
                    settings.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(settings);
                }

                importer.SaveAndReimport();
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (textures[i] == null || textures[i].width != atlases[i].width || textures[i].height != atlases[i].height)
                    throw new InvalidOperationException($"字体图集导入失败或尺寸发生变化: {texturePath}");
            }

            return textures;
        }

        private static void WriteAtlasPng(Texture2D atlas, string texturePath)
        {
            // 在 CPU 上显式写入 RGBA32，确保导出的 PNG 包含 TMP Shader 使用的 Alpha 通道。
            var pngTexture = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false, true);
            try
            {
                pngTexture.SetPixels32(atlas.GetPixels32());
                byte[] bytes = pngTexture.EncodeToPNG();
                if (bytes == null || bytes.Length == 0)
                    throw new InvalidOperationException($"字体图集 PNG 编码失败: {texturePath}");
                File.WriteAllBytes(texturePath, bytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pngTexture);
            }
        }

        /// <summary>记录包括材质预设在内的旧图集引用，重新生成后按图集索引重新绑定。</summary>
        public static Dictionary<Material, int> FindMaterials(Texture2D[] atlases)
        {
            var materials = new Dictionary<Material, int>();
            if (atlases.Length == 0) return materials;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    if (!(asset is Material material) || !material.HasProperty(ShaderUtilities.ID_MainTex)) continue;
                    Texture texture = material.GetTexture(ShaderUtilities.ID_MainTex);
                    if (texture == null) continue;
                    for (int i = 0; i < atlases.Length; i++)
                    {
                        if (texture != atlases[i]) continue;
                        materials[material] = i;
                        break;
                    }
                }
            }
            return materials;
        }
    }
}
