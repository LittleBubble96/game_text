using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Data;

namespace GameLogic.GamePlay.CorePlay
{
    /// <summary>
    /// CorePlay 玩法数据层 —— 纯数据与逻辑，不依赖 Unity GameObject
    /// </summary>
    public class CorePlayGamePlay : IGamePlay
    {
        // ================ 数据状态 ================

        private LevelDataConfigParse _levelConfig;
        private TextLevelData _currentLevelData;
        private int _currentLevelIndex = -1;
        private HashSet<int> _selectedStrokeIndices = new HashSet<int>();
        private HashSet<int> _foundAnswerIndices = new HashSet<int>();
        private bool _isGameRunning;

        // ================ 事件 ================

        /// <summary>笔画选中状态变化 (strokeIndex, isSelected)</summary>
        public event Action<int, bool> OnStrokeSelectionChanged;

        /// <summary>答案提交结果 (success, answerCharacter, errorMessage)</summary>
        public event Action<bool, string, string> OnAnswerSubmitted;

        /// <summary>关卡通关</summary>
        public event Action<int> OnLevelCompleted;

        /// <summary>关卡加载完成</summary>
        public event Action<TextLevelData> OnLevelLoaded;

        // ================ 属性 ================

        public bool IsGameRunning => _isGameRunning;
        public int CurrentLevelIndex => _currentLevelIndex;
        public TextLevelData CurrentLevelData => _currentLevelData;
        public IReadOnlyCollection<int> SelectedStrokeIndices => _selectedStrokeIndices;
        public IReadOnlyCollection<int> FoundAnswerIndices => _foundAnswerIndices;
        public int TotalLevelCount => _levelConfig?.LevelCount ?? 0;

        // ================ 初始化 ================

        public void Initialize(LevelDataConfigParse levelConfig)
        {
            _levelConfig = levelConfig;
        }

        public void StartGame()
        {
            _isGameRunning = true;
        }

        public void EndGame()
        {
            _isGameRunning = false;
            _selectedStrokeIndices.Clear();
        }

        public bool IsGameOver()
        {
            return !_isGameRunning;
        }

        // ================ 关卡加载 ================

        /// <summary>加载指定关卡（从缓存恢复时使用）</summary>
        public void LoadLevel(int levelIndex)
        {
            LoadLevelInternal(levelIndex, null);
        }

        /// <summary>加载关卡并恢复已找到的答案</summary>
        public void LoadLevel(int levelIndex, List<int> restoredFoundAnswers)
        {
            LoadLevelInternal(levelIndex, restoredFoundAnswers);
        }

        private void LoadLevelInternal(int levelIndex, List<int> restoredFoundAnswers)
        {
            if (_levelConfig == null)
            {
                DebugLogError("LevelConfig 未初始化");
                return;
            }

            if (levelIndex < 0 || levelIndex >= _levelConfig.LevelCount)
            {
                DebugLogError($"关卡索引超出范围: {levelIndex}/{_levelConfig.LevelCount}");
                return;
            }

            _currentLevelIndex = levelIndex;
            _currentLevelData = _levelConfig.GetLevelData(levelIndex);
            _selectedStrokeIndices.Clear();
            _foundAnswerIndices.Clear();

            // 恢复已找到的答案
            if (restoredFoundAnswers != null && restoredFoundAnswers.Count > 0)
            {
                foreach (int idx in restoredFoundAnswers)
                {
                    if (idx >= 0 && idx < _currentLevelData.answers.Count)
                        _foundAnswerIndices.Add(idx);
                }
            }

            _isGameRunning = true;

            DebugLog($"加载关卡 {levelIndex}: 基字『{_currentLevelData.baseCharacter}』, 共 {_currentLevelData.answers.Count} 个答案, 已找到 {_foundAnswerIndices.Count} 个");
            OnLevelLoaded?.Invoke(_currentLevelData);

            // 如果已经全部完成，直接通关
            if (IsLevelComplete())
            {
                CompleteLevel();
            }
        }

        // ================ 笔画操作 ================

        /// <summary>切换笔画的选中状态</summary>
        public void ToggleStroke(int strokeIndex)
        {
            if (!_isGameRunning || _currentLevelData == null) return;

            if (_selectedStrokeIndices.Contains(strokeIndex))
            {
                _selectedStrokeIndices.Remove(strokeIndex);
                OnStrokeSelectionChanged?.Invoke(strokeIndex, false);
            }
            else
            {
                _selectedStrokeIndices.Add(strokeIndex);
                OnStrokeSelectionChanged?.Invoke(strokeIndex, true);
            }
        }

        /// <summary>清除所有选中的笔画</summary>
        public void ClearSelection()
        {
            if (_selectedStrokeIndices.Count == 0) return;

            var indices = _selectedStrokeIndices.ToList();
            _selectedStrokeIndices.Clear();
            foreach (int idx in indices)
            {
                OnStrokeSelectionChanged?.Invoke(idx, false);
            }
        }

        // ================ 提交答案 ================

        /// <summary>提交当前选中的笔画作为答案</summary>
        public void SubmitAnswer()
        {
            if (!_isGameRunning || _currentLevelData == null)
            {
                OnAnswerSubmitted?.Invoke(false, "", "游戏未运行");
                return;
            }

            if (_selectedStrokeIndices.Count == 0)
            {
                OnAnswerSubmitted?.Invoke(false, "", "请先选择笔画");
                return;
            }

            // 将选中的笔画索引排序以便比较
            List<int> selectedSorted = _selectedStrokeIndices.OrderBy(i => i).ToList();

            // 遍历所有答案，检查是否匹配
            for (int ansIdx = 0; ansIdx < _currentLevelData.answers.Count; ansIdx++)
            {
                // 已找到的答案跳过
                if (_foundAnswerIndices.Contains(ansIdx)) continue;

                LevelAnswer answer = _currentLevelData.answers[ansIdx];

                // 检查该答案的每一组笔画组合
                foreach (StrokeSet set in answer.strokeSets)
                {
                    List<int> setSorted = set.strokeIndices.OrderBy(i => i).ToList();
                    if (setSorted.SequenceEqual(selectedSorted))
                    {
                        // 匹配成功！
                        OnAnswerFound(ansIdx, answer.answerCharacter);
                        return;
                    }
                }
            }

            // 检查是否选择了已经找到的答案（重复提交）
            for (int ansIdx = 0; ansIdx < _currentLevelData.answers.Count; ansIdx++)
            {
                if (!_foundAnswerIndices.Contains(ansIdx)) continue;

                LevelAnswer answer = _currentLevelData.answers[ansIdx];
                foreach (StrokeSet set in answer.strokeSets)
                {
                    List<int> setSorted = set.strokeIndices.OrderBy(i => i).ToList();
                    if (setSorted.SequenceEqual(selectedSorted))
                    {
                        OnAnswerSubmitted?.Invoke(false, answer.answerCharacter, "该答案已找到");
                        return;
                    }
                }
            }

            // 未匹配任何答案
            OnAnswerSubmitted?.Invoke(false, "", "所选笔画组合不正确");
        }

        private void OnAnswerFound(int answerIndex, string answerCharacter)
        {
            _foundAnswerIndices.Add(answerIndex);
            _selectedStrokeIndices.Clear();

            // 清除所有笔画高亮
            OnAnswerSubmitted?.Invoke(true, answerCharacter,
                $"正确！『{answerCharacter}』 ({_foundAnswerIndices.Count}/{GetRequiredAnswerCount()})");

            // 检查是否通关
            if (IsLevelComplete())
            {
                CompleteLevel();
            }
        }

        // ================ 通关判断 ================

        /// <summary>获取当前关卡所需的答案个数</summary>
        public int GetRequiredAnswerCount()
        {
            if (_currentLevelData == null) return 0;
            if (_currentLevelData.requiredAnswerCount > 0)
                return _currentLevelData.requiredAnswerCount;
            return _currentLevelData.answers.Count;
        }

        /// <summary>当前关卡是否完成</summary>
        public bool IsLevelComplete()
        {
            if (_currentLevelData == null) return false;
            return _foundAnswerIndices.Count >= GetRequiredAnswerCount();
        }

        private void CompleteLevel()
        {
            _isGameRunning = false;
            DebugLog($"关卡 {_currentLevelIndex} 通关!");
            OnLevelCompleted?.Invoke(_currentLevelIndex);
        }

        // ================ 存档 ================

        /// <summary>获取当前存档数据</summary>
        public CorePlaySaveData GetSaveData()
        {
            var data = new CorePlaySaveData { currentLevelIndex = _currentLevelIndex };
            data.levelProgresses.Add(new LevelSaveData
            {
                levelIndex = _currentLevelIndex,
                foundAnswerIndices = _foundAnswerIndices.ToList()
            });
            return data;
        }

        /// <summary>获取所有关卡的进度（用于批量保存）</summary>
        public void ApplyToRestore(CorePlayRestore restore)
        {
            restore.SetCurrentLevel(_currentLevelIndex);
            restore.SetFoundAnswers(_currentLevelIndex, _foundAnswerIndices.ToList());
        }

        // ================ 关卡信息 ================

        /// <summary>获取当前关卡的基字</summary>
        public string GetBaseCharacter()
        {
            return _currentLevelData?.baseCharacter ?? "";
        }

        /// <summary>获取当前关卡名称</summary>
        public string GetLevelName()
        {
            if (_currentLevelData == null) return "未加载";
            return $"第{_currentLevelData.level}关 - 『{_currentLevelData.baseCharacter}』";
        }

        /// <summary>是否有下一关</summary>
        public bool HasNextLevel()
        {
            return _currentLevelIndex + 1 < TotalLevelCount;
        }

        /// <summary>获取已找到的答案字符列表</summary>
        public List<string> GetFoundAnswerCharacters()
        {
            var result = new List<string>();
            if (_currentLevelData == null) return result;

            foreach (int idx in _foundAnswerIndices)
            {
                if (idx >= 0 && idx < _currentLevelData.answers.Count)
                    result.Add(_currentLevelData.answers[idx].answerCharacter);
            }
            return result;
        }

        // ================ 工具 ================

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void DebugLog(string msg)
        {
            UnityEngine.Debug.Log($"[CorePlayGamePlay] {msg}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void DebugLogError(string msg)
        {
            UnityEngine.Debug.LogError($"[CorePlayGamePlay] {msg}");
        }
    }
}