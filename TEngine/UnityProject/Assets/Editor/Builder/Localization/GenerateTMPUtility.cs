using System;
using System.Reflection;
using TEngine;
using TMPro;
using UnityEngine;

namespace Builder
{
    public static class GenerateTMPUtility
    {
        private static readonly FieldInfo AtlasTextureCache = typeof(TMP_FontAsset).GetField(
            "m_AtlasTexture", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>TMP 3.0 会缓存首张图集，替换数组时必须同步清空缓存。</summary>
        public static void SetAtlasTextures(TMP_FontAsset fontAsset, Texture2D[] textures)
        {
            if (AtlasTextureCache == null)
                throw new MissingFieldException(typeof(TMP_FontAsset).FullName, "m_AtlasTexture");

            fontAsset.atlasTextures = textures;
            AtlasTextureCache.SetValue(fontAsset, null);
        }

        /// <summary>
        /// 更新字体材质属性
        /// </summary>
        /// <param name="m"></param>
        /// <param name="fontAsset"></param>
        public static void UpdateMaterialProperty(Material m, TMP_FontAsset fontAsset, Texture2D atlasTexture = null)
        {
            if (atlasTexture == null) atlasTexture = fontAsset.atlasTextures[0];
            m.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
            m.SetFloat(ShaderUtilities.ID_TextureWidth, atlasTexture.width);
            m.SetFloat(ShaderUtilities.ID_TextureHeight, atlasTexture.height);

            int spread = fontAsset.atlasPadding + 1;
            m.SetFloat(ShaderUtilities.ID_GradientScale, spread); // Spread = Padding for Brute Force SDF.

            m.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
            m.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);
        }
        
        /// <summary>
        /// 设置纹理的isReadable属性
        /// </summary>
        /// <param name="texture">要修改的纹理</param>
        /// <param name="isReadable">是否设置为可读</param>
        public static void SetTextureReadable(Texture2D texture, bool isReadable)
        {
            Type utilityType = Type.GetType("UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities,UnityEditor.TextCoreFontEngineModule");
            if (utilityType == null)
            {
                Log.Error("fail to find FontEngineEditorUtilities");
            }
            else
            {
                MethodInfo method = utilityType.GetMethod(
                    "SetAtlasTextureIsReadable",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(Texture2D), typeof(bool) },
                    null
                );

                if (method != null)
                {
                    method.Invoke(null, new object[] { texture, isReadable });
                    return;
                }
                else
                {
                    Log.Error("fail to find SetAtlasTextureIsReadable!");
                }
            }
        }
    }
}
