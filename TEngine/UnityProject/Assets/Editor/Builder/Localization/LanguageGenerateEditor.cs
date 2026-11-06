using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using GameConfig.language;
using GameLogic.Data;
using GameLogic.GamePlay.CorePlay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Builder
{
    /// <summary>
    /// 多语言 SDF 字体自动生成工具。
    /// 核心思路：以「字体文件」为维度，而非以「语言」为维度。
    /// 一种字体(.ttf)可能被多种语言共用（如中日文共用同一款字体），
    /// 我们只生成一份 SDF 资源，包含所有使用该字体的语言中出现的全部字符。
    /// 设置面板与普通 UI 共用主字体，关卡文字仍生成独立字体资源。
    /// </summary>
    public static class LanguageGenerateEditor
    {
        #region 可调参数（按需修改）

        /// <summary>字体采样字号（值越大字形越清晰，图集消耗也越大）</summary>
        private const int SamplingPointSize = 60;

        /// <summary>图集内边距（像素），防止 SDF 边缘溢出到相邻字形</summary>
        private const int AtlasPadding = 6;

        /// <summary>SDF 渲染模式</summary>
        private const GlyphRenderMode RenderMode = GlyphRenderMode.SDF8;

        /// <summary>源字体(.ttf)所在目录</summary>
        private const string SourceFontDir = "Assets/AssetArt/Font";

        /// <summary>普通字体 SDF 资源输出目录</summary>
        private const string OutputFontDir = "Assets/AssetRaw/Fonts";

        /// <summary>关卡字体输出子目录</summary>
        private const string LevelSubDir = "Level";

        /// <summary>关卡字体名称后缀</summary>
        private const string LevelSuffix = "_Level";

        /// <summary>SDF 字体使用的 Shader 名称</summary>
        private const string SDFShaderName = "TextMeshPro/Distance Field";

        /// <summary>图集最大尺寸（CJK 大字集建议 4096）</summary>
        private const int AtlasMaxSize = 4096;

        /// <summary>完整预烘焙的静态字体导出独立 PNG，并启用平台纹理压缩。</summary>
        private const bool CompressFontAtlas = true;

        /// <summary>Android、iOS、WebGL 图集格式；调整后需重新生成并检查小字号和描边。</summary>
        private const TextureImporterFormat AtlasCompressionFormat = TextureImporterFormat.ASTC_6x6;

        private const int AtlasCompressionQuality = 100;

        #endregion

        // ================================================================
        //  主入口
        // ================================================================

        [MenuItem("TEngine/Localization/测试", priority = -100)]
        public static void Generate_Test()
        {
            BuildFontAsset("AaFengKuangYuanShiRen", new HashSet<char>("A"),
                OutputFontDir + "/Test", string.Empty);
        }
        
        /// <summary>
        /// 菜单入口：TEngine -> Localization -> 生成语言文件。
        /// 1) 加载配置表
        /// 2) 将语言按「字体文件」分组
        /// 3) 每组收集其涵盖的所有多语言文本的唯一字符
        /// 4) 为每组字体生成主 UI 字体（包含设置面板），并单独生成关卡字体
        /// 5) 刷新资源数据库
        /// </summary>
        [MenuItem("TEngine/Localization/生成语言文件", priority = -100)]
        public static void Generate()
        {
            try
            {
                // ---- 1. 加载编辑器配置表 ----
                EditorUtility.DisplayProgressBar("多语言字体生成", "加载配置表 ...", 0f);
                ConfigSystem.Instance.LoadEditor();

                var langTable   = ConfigSystem.Instance.Tables.TbLanguage;
                var contentTable = ConfigSystem.Instance.Tables.TbLanguageContent;

                if (langTable == null || langTable.DataMap.Count == 0)
                {
                    ShowError("未找到语言配置数据，请先执行 TEngine/Luban/转表。");
                    return;
                }
                if (contentTable == null || contentTable.DataMap.Count == 0)
                {
                    ShowError("未找到多语言文本数据，请先执行 TEngine/Luban/转表。");
                    return;
                }

                // ---- 2. 按字体分组语言 ----
                EditorUtility.DisplayProgressBar("多语言字体生成", "按字体分组语言 ...", 0.05f);
                Dictionary<string, List<ConfLanguage>> fontGroups = GroupLanguagesByFont(langTable);

                if (fontGroups.Count == 0)
                {
                    ShowError("所有语言均未配置字体文件名 (Font 字段为空)，无法生成。");
                    return;
                }
                HashSet<char> levelChars = CollectLevelCharsForFontGroup();

                var languageNameKeys = new HashSet<string>(langTable.DataMap.Values
                    .Select(language => language.Name).Where(name => !string.IsNullOrEmpty(name)));
                // ---- 3 & 4. 逐字体收集全部 UI 字符并生成 SDF 资源 ----
                float totalSteps = fontGroups.Count * 2f;   // 每种字体：收集字符 + 生成主字体
                int   step = 0;

                foreach (var kv in fontGroups)
                {
                    string fontFileName    = kv.Key;
                    List<ConfLanguage> langs = kv.Value;

                    // 3a. 收集全部 UI 多语言，包括设置文案和语言选择项。
                    step++;
                    float progress = 0.1f + 0.85f * (step / totalSteps);
                    EditorUtility.DisplayProgressBar("多语言字体生成", $"分析字符 [{fontFileName}] 普通文本 ...", progress);

                    HashSet<char> normalChars = CollectCharsForFontGroup(langs, contentTable);
                    HashSet<char> settingChars = CollectCharsForFontGroup(langs, contentTable,
                        key => key.StartsWith("setting_", StringComparison.Ordinal) || languageNameKeys.Contains(key));

                    if (fontFileName.Equals("TaiWanQuanZiKuZhengKaiTi-2"))
                    {
                        normalChars.ExceptWith(levelChars);
                    }
                    // 设置文案、语言名称及白名单直接进入主字库，不依赖关卡字库回退。
                    normalChars.UnionWith(settingChars);
                    // 3b. 生成普通字体 SDF 资源
                    step++;
                    progress = 0.1f + 0.85f * (step / totalSteps);
                    EditorUtility.DisplayProgressBar("多语言字体生成", $"生成 [{fontFileName}] SDF 字体 ...", progress);

                    if (normalChars.Count > 0)
                    {
                        BuildFontAsset(fontFileName, normalChars, OutputFontDir, string.Empty);
                    }
                    else
                    {
                        Debug.LogWarning($"[字体生成] [{fontFileName}] 无普通文本字符，跳过。");
                    }

                }

                // ---- 4.5 收集关卡文字并生成独立的关卡字体资源 ----
                EditorUtility.DisplayProgressBar("多语言字体生成", "收集关卡文字 ...", 0.93f);
                
                if (levelChars.Count > 0)
                {
                    string levelDir = Path.Combine(OutputFontDir, LevelSubDir);
                    EnsureDirectory(levelDir);
                
                    // 关卡文字使用主游戏字体，优先匹配 AlibabaPuHuiTi-3-95-ExtraBold
                    string levelFont = "TaiWanQuanZiKuZhengKaiTi-2";
                
                    BuildFontAsset(levelFont, levelChars, levelDir, LevelSuffix);
                }
                else
                {
                    Debug.LogWarning("[字体生成] 无关卡文字字符，跳过关卡字体生成。");
                }

                // ---- 5. 保存并刷新 ----
                EditorUtility.DisplayProgressBar("多语言字体生成", "刷新资源数据库 ...", 0.98f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
                Debug.Log("[字体生成] 全部 SDF 字体生成完毕！");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[字体生成] 异常:\n{ex}");
                EditorUtility.DisplayDialog("字体生成失败", ex.Message, "确定");
            }
        }

        // ================================================================
        //  字体分组
        // ================================================================

        /// <summary>
        /// 将 TbLanguage 中所有语言按 Font 字段分组。
        /// 多种语言可能共用同一款 .ttf 字体（如中、日文共用），
        /// 我们只为每个字体文件生成一份 SDF 资源。
        /// </summary>
        /// <returns>Key = 字体文件名（如 "AaFengKuangYuanShiRen.ttf"），Value = 使用该字体的语言列表</returns>
        private static Dictionary<string, List<ConfLanguage>> GroupLanguagesByFont(
            TbLanguage langTable)
        {
            var groups = new Dictionary<string, List<ConfLanguage>>();

            foreach (var kv in langTable.DataMap)
            {
                ConfLanguage cfg = kv.Value;
                string font = cfg.Font;

                // 跳过未配置字体的语言
                if (string.IsNullOrEmpty(font))
                {
                    Debug.LogWarning($"[字体分组] 语言 [{cfg.Name}] 未配置 Font 字段，跳过。");
                    continue;
                }

                if (!groups.TryGetValue(font, out var list))
                {
                    list = new List<ConfLanguage>();
                    groups[font] = list;
                }

                list.Add(cfg);
            }

            // 日志：输出分组结果
            foreach (var kv in groups)
            {
                var names = new List<string>();
                foreach (var l in kv.Value) names.Add(l.Name);
                Debug.Log($"[字体分组] 字体 [{kv.Key}] 被以下语言使用: {string.Join(", ", names)}");
            }

            return groups;
        }

        // ================================================================
        //  字符收集（按字体维度）
        // ================================================================

        /// <summary>
        /// 为某个字体所覆盖的所有语言，收集其多语言文本中出现的全部唯一字符。
        /// 此方法只做纯数据提取，不涉及任何资源加载或生成逻辑，确保低耦合。
        /// </summary>
        /// <param name="langs">使用该字体的语言配置列表</param>
        /// <param name="contentTable">多语言内容表</param>
        /// <param name="includeKey">可选的 key 筛选条件，省略时收集全部 UI 文本</param>
        /// <returns>去重后的字符集合</returns>
        private static HashSet<char> CollectCharsForFontGroup(
            List<ConfLanguage> langs,
            TbLanguageContent contentTable,
            Func<string, bool> includeKey = null)
        {
            HashSet<char> charSet = new HashSet<char>();

            // 遍历所有多语言内容条目
            foreach (var contentEntry in contentTable.DataMap)
            {
                if (includeKey != null && !includeKey(contentEntry.Key)) continue;

                string[] values = contentEntry.Value.Value;

                // 遍历使用该字体的每种语言，按 LanguageCode 取对应索引的文本
                foreach (var langCfg in langs)
                {
                    int idx = (int)langCfg.LanguageCode - 1;
                    if (idx < 0 || idx >= values.Length) continue;

                    string text = values[idx];
                    if (string.IsNullOrEmpty(text)) continue;

                    foreach (char c in text)
                        charSet.Add(c);
                }
            }
            //添加白名单
            foreach (var writeChar in GenerateTMPDefine.WhiteList)
            {
                charSet.Add(writeChar);
            }
            return charSet;
        }
        
        /// <summary>
        /// 获取关卡得文字 生成一个单独得字体
        /// </summary>
        /// <returns></returns>
        private static HashSet<char> CollectLevelCharsForFontGroup()
        {
            HashSet<char> chars = new HashSet<char>();
            TextLevelDataScriptableObject levelDataScriptable = AssetDatabase.LoadAssetAtPath<TextLevelDataScriptableObject>(
                "Assets/AssetRaw/Configs/LevelConfigs/TextLevelDataScriptableObject.asset");
            foreach (var level in levelDataScriptable.levelDataList)
            {
                foreach (var c in level.baseCharacter)
                {
                    chars.Add(c);
                }

                foreach (var answer in level.answers)
                {
                    foreach (var c in answer.answerCharacter)
                    {
                        chars.Add(c);
                    }
                }
            }
            //添加白名单
            foreach (var writeChar in GenerateTMPDefine.WhiteList)
            {
                chars.Add(writeChar);
            }

            //添加韵母
            foreach (var intonation in GenerateTMPDefine.LevelIntonation)
            {
                chars.Add(intonation);
            }
            return chars;
        }
        
        /// <summary>
        /// 按顺序排序
        /// </summary>
        /// <param name="chars"></param>
        /// <returns></returns>
        private static string GetAllCharsByString(HashSet<char> chars)
        {
            var str = string.Concat(chars.OrderBy(c => (int)c));
            return str;
        }

        // ================================================================
        //  字体 SDF 资源生成
        // ================================================================

        /// <summary>
        /// 加载一款源字体（.ttf），创建对应的 TMPro SDF FontAsset 并保存为 .asset 文件。
        /// 先校验并移除字体中不存在的字符，再以 256×256 起步的图集预烘焙项目相关字符，
        /// 若图集装不下则逐步扩容重试（256→512→1024→...→4096）。
        /// 最终使用 Dynamic 模式兜底——运行时仍可动态生成缺失字形。
        /// 
        /// 生成流程参照 TMPro 标准做法：
        ///   - 静态图集导出 PNG 并压缩，动态兜底图集保持内嵌和可写
        ///   - 更新时保留字体/主材质引用，并重新绑定图集和材质预设
        ///   - 收尾：ReadFontAssetDefinition → ImportAsset → ON_FONT_PROPERTY_CHANGED
        /// 
        /// 注意：一种 .ttf 只生成一份 SDF 资源。
        /// 若多种语言共用同一字体，它们共享这份资源。
        /// 语言级别的差异（如 BaseLine）应在 UI 组件层面处理，而非字体资源层面。
        /// </summary>
        /// <param name="fontFileName">字体文件名（如 "AaFengKuangYuanShiRen.ttf"）</param>
        /// <param name="characters">该字体需要支持的全部唯一字符（会被原地修改：移除字体不存在的字符）</param>
        /// <param name="outDir">输出目录</param>
        /// <param name="nameSuffix">资源名后缀（"" 或 "_Level"）</param>
        private static void BuildFontAsset(string fontFileName, HashSet<char> characters,
            string outDir, string nameSuffix)
        {
            if (characters == null || characters.Count == 0) return;

            // -- 加载源字体（配置表可能不带扩展名，自动补 .ttf） --
            string fontFile = fontFileName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                ? fontFileName : fontFileName + ".ttf";
            string srcPath = Path.Combine(SourceFontDir, fontFile);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(srcPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[字体生成] 源字体缺失: {srcPath}");
                return;
            }

            // -- 计算资源名与路径 --
            string fontBaseName = Path.GetFileNameWithoutExtension(fontFileName);
            string assetName    = $"{fontBaseName}{nameSuffix} SDF.asset";
            string assetPath    = Path.Combine(outDir, assetName).Replace('\\', '/');
            string assetCoreName = Path.GetFileNameWithoutExtension(assetName);

            // -- 校验并移除字体中不存在的字符（避免 TryAddCharacters 因无字形字符无限扩容） --
            int removedCount = RemoveMissingGlyphs(sourceFont, characters, fontBaseName, nameSuffix);
            if (removedCount > 0)
            {
                Debug.LogWarning($"[字体生成] '{fontBaseName}{nameSuffix}' 已移除 {removedCount} 个字体不存在的字符，剩余 {characters.Count} 个。");
            }

            if (characters.Count == 0)
            {
                Debug.LogWarning($"[字体生成] '{fontBaseName}{nameSuffix}' 移除缺失字形后无可用字符，跳过生成。");
                return;
            }

            // -- 将字符集转为字符串（按 Unicode 排序，确保 TryAddCharacters 顺序稳定） --
            string charString = GetAllCharsByString(characters);

            // -- 创建字体，从 256×256 起步逐步扩容，最大 4096 --
            const int startAtlasSize = 256;
            if (!TryGenerateFontAsset(out TMP_FontAsset fontAsset, sourceFont,
                    charString, startAtlasSize, startAtlasSize,
                    AtlasPadding, SamplingPointSize, fontBaseName, nameSuffix))
            {
                Debug.LogError($"[字体生成] 创建 FontAsset 失败: {assetName}");
                return;
            }

            SaveFontAsset(fontAsset, assetPath, assetCoreName);
            Debug.Log($"[字体生成] ✔ {assetPath}  |  字符数={characters.Count}");
        }

        private static void SaveFontAsset(TMP_FontAsset generatedFont, string assetPath, string assetCoreName)
        {
            EnsureDirectory(Path.GetDirectoryName(assetPath));
            generatedFont.name = assetCoreName;
            Texture2D[] generatedAtlases = generatedFont.atlasTextures;
            Material generatedMaterial = generatedFont.material;
            TMP_FontAsset fontAsset = generatedFont;
            try
            {
                TMP_FontAsset oldFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                Texture2D[] oldAtlases = oldFontAsset != null
                    ? (Texture2D[])oldFontAsset.atlasTextures.Clone() : Array.Empty<Texture2D>();
                Dictionary<Material, int> materials = TMPAtlasConverter.FindMaterials(oldAtlases);

                bool isStatic = generatedFont.atlasPopulationMode == AtlasPopulationMode.Static;
                Texture2D[] atlases = CompressFontAtlas && isStatic
                    ? TMPAtlasConverter.ExportAtlases(generatedFont, assetPath, AtlasCompressionFormat,
                        AtlasMaxSize, AtlasCompressionQuality)
                    : generatedAtlases;

                if (oldFontAsset != null)
                {
                    Material material = oldFontAsset.material;
                    var fallbacks = new List<TMP_FontAsset>(oldFontAsset.fallbackFontAssetTable);
                    EditorUtility.CopySerialized(generatedFont, oldFontAsset);
                    oldFontAsset.material = material != null ? material : generatedMaterial;
                    oldFontAsset.fallbackFontAssetTable = fallbacks;
                    fontAsset = oldFontAsset;
                }
                else
                {
                    generatedMaterial.shader = Shader.Find(SDFShaderName);
                    AssetDatabase.CreateAsset(fontAsset, assetPath);
                }

                GenerateTMPUtility.SetAtlasTextures(fontAsset, atlases);
                if (!AssetDatabase.Contains(fontAsset.material))
                {
                    fontAsset.material.name = $"{assetCoreName} Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                for (int i = 0; i < atlases.Length; i++)
                {
                    if (!AssetDatabase.Contains(atlases[i]))
                    {
                        atlases[i].name = $"{assetCoreName} Atlas {i}";
                        AssetDatabase.AddObjectToAsset(atlases[i], fontAsset);
                    }
                    // 动态兜底仍需 FontEngine 写入，不能关闭可读性。
                    if (isStatic && !CompressFontAtlas)
                        GenerateTMPUtility.SetTextureReadable(atlases[i], false);
                }

                GenerateTMPUtility.UpdateMaterialProperty(fontAsset.material, fontAsset);
                EditorUtility.SetDirty(fontAsset.material);
                foreach (var pair in materials)
                {
                    if (pair.Key == null) continue;
                    int atlasIndex = pair.Value < atlases.Length ? pair.Value : 0;
                    GenerateTMPUtility.UpdateMaterialProperty(pair.Key, fontAsset, atlases[atlasIndex]);
                    EditorUtility.SetDirty(pair.Key);
                }

                // 仅删除当前字体拥有的旧内嵌图集；外部 PNG 保留 GUID，后续生成直接覆盖内容。
                foreach (Texture2D oldAtlas in oldAtlases)
                {
                    if (oldAtlas != null && !atlases.Contains(oldAtlas) && AssetDatabase.IsSubAsset(oldAtlas)
                        && AssetDatabase.GetAssetPath(oldAtlas) == assetPath)
                        UnityEngine.Object.DestroyImmediate(oldAtlas, true);
                }

                if (isStatic)
                {
                    var serializedFont = new SerializedObject(fontAsset);
                    SerializedProperty sourceFont = serializedFont.FindProperty("m_SourceFontFile_EditorRef");
                    if (sourceFont != null)
                    {
                        sourceFont.objectReferenceValue = null;
                        serializedFont.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                fontAsset.ReadFontAssetDefinition();
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);
                TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
            }
            finally
            {
                // 导出 PNG 或更新已有字体后，清理不再使用的临时生成对象。
                foreach (Texture2D atlas in generatedAtlases)
                    if (atlas != null && !AssetDatabase.Contains(atlas)) UnityEngine.Object.DestroyImmediate(atlas);
                if (generatedMaterial != null && !AssetDatabase.Contains(generatedMaterial))
                    UnityEngine.Object.DestroyImmediate(generatedMaterial);
                if (generatedFont != null && !AssetDatabase.Contains(generatedFont))
                    UnityEngine.Object.DestroyImmediate(generatedFont);
            }
        }

        /// <summary>
        /// 尝试创建 SDF 字体资源并预烘焙指定字符。
        /// 从给定的 atlasWidth/Height 开始，若字符无法全部装入图集，
        /// 则逐步扩容（交替扩宽/扩高，256×256→512×256→512×512→...→4096×4096）。
        /// 最终仍失败则回退到 Dynamic 模式（运行时按需生成）。
        /// </summary>
        /// <returns>true = 成功创建 FontAsset（至少尝试了添加字符）</returns>
        private static bool TryGenerateFontAsset(out TMP_FontAsset fontAsset, Font sourceFont,
            string charString, int atlasWidth, int atlasHeight,
            int padding, int samplingPointSize,
            string fontBaseName, string nameSuffix)
        {
            fontAsset = null;
            int currentW = atlasWidth;
            int currentH = atlasHeight;

            while (true)
            {
                // 如果之前创建的失败资源还在，需要销毁
                if (fontAsset != null)
                {
                    foreach (Texture2D atlas in fontAsset.atlasTextures)
                        UnityEngine.Object.DestroyImmediate(atlas);
                    UnityEngine.Object.DestroyImmediate(fontAsset.material);
                    UnityEngine.Object.DestroyImmediate(fontAsset);
                    fontAsset = null;
                }

                // 创建 TMP_FontAsset（Dynamic 模式兜底）
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    samplingPointSize,
                    padding,
                    RenderMode,
                    currentW,
                    currentH,
                    AtlasPopulationMode.Dynamic,
                    false
                );

                // 创建失败，直接返回
                if (fontAsset == null)
                {
                    Debug.LogError($"[字体生成] TMP_FontAsset.CreateFontAsset 返回 null，" +
                                   $"图集={currentW}×{currentH}");
                    return false;
                }

                // 尝试添加项目相关字符
                if (fontAsset.TryAddCharacters(charString, out var missingChars))
                {
                    // 全部字符装入成功
                    fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
                    Debug.Log($"[字体生成] '{fontBaseName}{nameSuffix}' 字符预烘焙成功，" +
                               $"图集={currentW}×{currentH}，字符数={charString.Length}");
                    return true;
                }

                // 图集大小不足，逐步扩容（交替扩宽/扩高，256×256→512×256→512×512→...→4096×4096）
                if (currentW >= AtlasMaxSize && currentH >= AtlasMaxSize)
                {
                    // 已达到最大图集尺寸 4096×4096，无法继续扩容
                    break;
                }

                if (currentW <= currentH)
                {
                    // 宽度 ≤ 高度：扩宽
                    int newW = Math.Min(currentW * 2, AtlasMaxSize);
                    Debug.LogWarning($"[字体生成] '{fontBaseName}{nameSuffix}' 图集宽度不足，" +
                                     $"从 {currentW}×{currentH} 扩宽至 {newW}×{currentH} 重试 ...");
                    currentW = newW;
                }
                else
                {
                    // 高度 < 宽度：扩高
                    int newH = Math.Min(currentH * 2, AtlasMaxSize);
                    Debug.LogWarning($"[字体生成] '{fontBaseName}{nameSuffix}' 图集高度不足，" +
                                     $"从 {currentW}×{currentH} 扩高至 {currentW}×{newH} 重试 ...");
                    currentH = newH;
                }
            }

            // 图集已满时必须允许新图集，否则 Dynamic 也无法再添加缺失字符。
            if (fontAsset != null) fontAsset.isMultiAtlasTexturesEnabled = true;
            Debug.LogWarning($"[字体生成] '{fontBaseName}{nameSuffix}' 无法全量预烘焙字符，" +
                             $"图集={currentW}×{currentH}，将依赖 Dynamic 模式。");
            return fontAsset != null;
        }

        /// <summary>
        /// 通过 FontEngine 检测源字体中缺失字形的字符，并从字符集中移除。
        /// 避免 TryAddCharacters 尝试添加无字形字符导致不必要的图集扩容。
        /// </summary>
        /// <returns>被移除的字符数量</returns>
        private static int RemoveMissingGlyphs(Font sourceFont, HashSet<char> characters,
            string fontBaseName, string suffix)
        {
            FontEngineError err = FontEngine.LoadFontFace(sourceFont, SamplingPointSize);
            if (err != FontEngineError.Success)
            {
                Debug.LogWarning($"[字形校验] 无法加载字体 '{fontBaseName}{suffix}' 到 FontEngine ({err})，跳过校验。");
                return 0;
            }

            var missing = new List<char>();
            foreach (char c in characters)
            {
                if (!FontEngine.TryGetGlyphIndex((uint)c, out _))
                    missing.Add(c);
            }

            if (missing.Count == 0) return 0;

            // 从字符集中移除字体不存在的字符
            foreach (char c in missing)
                characters.Remove(c);

            // 日志：输出前 N 个缺失字符示例
            int maxShow = 30;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[字形校验] ⚠ '{fontBaseName}{suffix}' 字体缺少 {missing.Count} 个字形，已移除:");

            int showCount = Math.Min(missing.Count, maxShow);
            for (int i = 0; i < showCount; i++)
            {
                sb.Append($"  U+{(int)missing[i]:X4}({missing[i]})");
                if ((i + 1) % 5 == 0 || i == showCount - 1) sb.AppendLine();
            }

            if (missing.Count > maxShow)
                sb.AppendLine($"  ... 及其他 {missing.Count - maxShow} 个字符");

            Debug.LogWarning(sb.ToString());
            return missing.Count;
        }

        // ================================================================
        //  工具方法
        // ================================================================

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void ShowError(string msg)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[字体生成] {msg}");
            EditorUtility.DisplayDialog("字体生成", msg, "确定");
        }

    }
}
