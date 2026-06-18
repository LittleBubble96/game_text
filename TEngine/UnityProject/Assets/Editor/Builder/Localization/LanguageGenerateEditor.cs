using System.Collections.Generic;
using UnityEditor;

namespace Builder
{
    public static class LanguageGenerateEditor
    {
        [MenuItem("TEngine/Localization/生成语言文件", priority = -100)]
        public static void Generate()
        {
            ConfigSystem.Instance.LoadEditor();
            // font 资源路径在  Assets/AssetArt/Font 下
            // 需要有个多语言导出设置，有配置属性 字体导出得路径等
            //获取语言对应得多语言文本
            //根据多语言文本创建tmp sdf 字体文件到：Assets/AssetRaw/Fonts 下 (需要有命名规范)
            //  图集生成 规则 {0, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 } 
            //   遇到没有的字 报错 和跳过
        }
        
        //获取 语言对应得文本集合
        private static Dictionary<string, List<string>> GetLanguageTexts()
        {
            ConfigSystem.Instance.LoadEditor();
            Dictionary<string, List<string>> languageTexts = new Dictionary<string, List<string>>();
            //多语言选择得key需要排除
            HashSet<string> blackList = new HashSet<string>();
            foreach (var item in ConfigSystem.Instance.Tables.TbLanguage.DataMap)
            {
                blackList.Add(item.Value.Name);
                if (languageTexts.ContainsKey(item.Value.Font))
                {
                    continue;
                }
                languageTexts.Add(item.Value.Font, new List<string>());
                foreach (var confLanguageContent in ConfigSystem.Instance.Tables.TbLanguageContent.DataMap)
                {
                    if (blackList.Contains(confLanguageContent.Key))
                    {
                        continue;
                    }
                    languageTexts[item.Value.Font].Add(confLanguageContent.Value.Value[(int)(item.Value.LanguageCode) - 1]);
                }
            }

            
            return languageTexts;
        }
    }
}