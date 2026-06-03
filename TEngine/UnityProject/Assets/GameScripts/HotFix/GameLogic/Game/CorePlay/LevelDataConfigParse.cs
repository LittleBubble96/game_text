using System.Collections.Generic;
using GameLogic.Data;
using UnityEngine;

namespace GameLogic.GamePlay.CorePlay
{
    /// <summary>
    /// 关卡数据加载与解析
    /// </summary>
    public class LevelDataConfigParse
    {
        private const string LevelDataResPath = "TextLevelDataScriptableObject";
        private const string GraphicDataResPath = "TextGraphicDataScriptableObject";

        private TextLevelDataScriptableObject _levelDataAsset;
        private TextGraphicDataScriptableObject _graphicDataAsset;

        /// <summary>所有关卡数据</summary>
        public List<TextLevelData> AllLevels { get; private set; }

        /// <summary>字形数据映射（字符 -> 图形数据）</summary>
        public Dictionary<string, TextGraphicData> GraphicDataMap { get; private set; }

        /// <summary>加载所有关卡配置</summary>
        public void LoadAllLevels()
        {
            _levelDataAsset = GameModule.Resource.LoadAsset<TextLevelDataScriptableObject>(LevelDataResPath);
            _graphicDataAsset = GameModule.Resource.LoadAsset<TextGraphicDataScriptableObject>(GraphicDataResPath);

            if (_levelDataAsset == null)
            {
                Debug.LogError($"未找到关卡数据: Resources/{LevelDataResPath}");
                AllLevels = new List<TextLevelData>();
            }
            else
            {
                AllLevels = new List<TextLevelData>(_levelDataAsset.levelDataList);
                Debug.Log($"加载了 {AllLevels.Count} 个关卡数据");
            }

            BuildGraphicDataMap();
        }

        private void BuildGraphicDataMap()
        {
            GraphicDataMap = new Dictionary<string, TextGraphicData>();
            if (_graphicDataAsset == null || _graphicDataAsset.TextGraphicDataList == null) return;

            foreach (var gd in _graphicDataAsset.TextGraphicDataList)
            {
                if (gd != null && !string.IsNullOrEmpty(gd.character))
                {
                    GraphicDataMap[gd.character] = gd;
                }
            }
            Debug.Log($"加载了 {GraphicDataMap.Count} 个字形数据");
        }

        /// <summary>根据关卡索引获取关卡数据</summary>
        public TextLevelData GetLevelData(int levelIndex)
        {
            if (AllLevels == null || levelIndex < 0 || levelIndex >= AllLevels.Count)
                return null;
            return AllLevels[levelIndex];
        }

        /// <summary>获取字符的图形数据</summary>
        public TextGraphicData GetGraphicData(string character)
        {
            if (GraphicDataMap == null || string.IsNullOrEmpty(character))
                return null;
            GraphicDataMap.TryGetValue(character, out var data);
            return data;
        }

        /// <summary>获取关卡总数</summary>
        public int LevelCount => AllLevels?.Count ?? 0;
    }
}