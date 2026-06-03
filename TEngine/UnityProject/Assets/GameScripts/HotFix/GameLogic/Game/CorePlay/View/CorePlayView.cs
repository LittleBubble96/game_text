using GameLogic.Data;
using GameLogic.GamePlay;
using GameLogic.View;
using TEngine;
using UnityEngine;

namespace GameLogic.GamePlay.CorePlay.View
{
    /// <summary>
    /// CorePlay 视图层 —— 动态创建 DrawCharacter、笔画渲染、视觉反馈
    /// 通过 IGamePlay 接口与数据层通信，实现数据-视图分离
    /// 输入由 StrokeInputHandler 统一做射线检测（编辑器鼠标 / 移动端 Touch）
    /// </summary>
    public class CorePlayView : MonoBehaviour
    {
        
        private Color _highlightColor = new Color(1f, 0.85f, 0.2f);
        private Color _defaultStrokeColor = Color.white;
        private float _highlightZOffset = -1f;

        // ================ 内部状态 ================

        private IGamePlay _gamePlay;
        private LevelDataConfigParse _levelConfig;
        private DrawCharacter _drawCharacter;
        private StrokeInputHandler _strokeInputHandler;
        private bool _isInitialized;

        // ================ 属性 ================


        // ================ 动态初始化 ================

        /// <summary>初始化视图，绑定数据层（通过 IGamePlay 接口）</summary>
        public void Initialize(IGamePlay gamePlay, LevelDataConfigParse levelConfig)
        {
            _gamePlay = gamePlay;
            _levelConfig = levelConfig;

            if (_gamePlay == null)
            {
                Debug.LogError("[CorePlayView] gamePlay 为 null");
                return;
            }

            // 动态创建 DrawCharacter
            CreateDrawCharacter();

            // 动态创建射线检测输入处理器
            CreateStrokeInputHandler();

            // 绑定数据层事件
            _gamePlay.OnLevelCompleted += OnLevelCompleted;
            // 从具体类型绑定 CorePlay 独有事件
            if (_gamePlay is CorePlayGamePlay corePlay)
            {
                corePlay.OnLevelLoaded += OnLevelLoaded;
                corePlay.OnStrokeSelectionChanged += OnStrokeSelectionChanged;
                corePlay.OnAnswerSubmitted += OnAnswerSubmitted;
            }

            _isInitialized = true;

            // 如果当前已有关卡数据，立刻渲染
            if (_gamePlay is CorePlayGamePlay cp && cp.CurrentLevelData != null)
            {
                RenderLevel(cp.CurrentLevelData);
            }
            Debug.Log("[CorePlayView] 初始化完成 (DrawCharacter + StrokeInputHandler 动态创建)");
        }

        private void CreateDrawCharacter()
        {
            var dcGo = new GameObject("DrawCharacter");
            dcGo.transform.SetParent(transform);
            _drawCharacter = dcGo.AddComponent<DrawCharacter>();
            _drawCharacter.DefaultStrokeColor = _defaultStrokeColor;
        }

        private void CreateStrokeInputHandler()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError("[CorePlayView] 未找到 Camera，笔画点击将不生效");
                    return;
                }
            }

            _strokeInputHandler = gameObject.AddComponent<StrokeInputHandler>();
            _strokeInputHandler.Initialize(_drawCharacter, cam, OnStrokeClicked);
        }

        private void OnDestroy()
        {
            if (_gamePlay != null)
            {
                _gamePlay.OnLevelCompleted -= OnLevelCompleted;
                if (_gamePlay is CorePlayGamePlay corePlay)
                {
                    corePlay.OnLevelLoaded -= OnLevelLoaded;
                    corePlay.OnStrokeSelectionChanged -= OnStrokeSelectionChanged;
                    corePlay.OnAnswerSubmitted -= OnAnswerSubmitted;
                }
            }
        }

        // ================ 关卡渲染 ================

        private void OnLevelLoaded(TextLevelData levelData)
        {
            RenderLevel(levelData);
        }

        /// <summary>渲染关卡：解析数据并绘制笔画</summary>
        public void RenderLevel(TextLevelData levelData)
        {
            if (_drawCharacter == null)
            {
                CreateDrawCharacter();
            }

            TextGraphicData graphicData = _levelConfig?.GetGraphicData(levelData.baseCharacter);
            if (graphicData == null)
            {
                Debug.LogError($"[CorePlayView] 未找到『{levelData.baseCharacter}』的字形数据");
                return;
            }

            _drawCharacter.PositionOffset = levelData.positionOffset;
            _drawCharacter.Draw(graphicData, showStrokeIndices: false);

            // 更新 StrokeInputHandler 引用（Draw 会重建子物体）
            if (_strokeInputHandler != null)
            {
                _strokeInputHandler.Initialize(_drawCharacter, Camera.main, OnStrokeClicked);
            }

            Debug.Log($"[CorePlayView] 渲染关卡: 『{levelData.baseCharacter}』, {graphicData.strokes.Count} 笔画");
        }

        // ================ 笔画点击（来自 StrokeInputHandler 的射线检测） ================

        private void OnStrokeClicked(int strokeIndex)
        {
            if (!_isInitialized || _gamePlay == null) return;
            _gamePlay.ToggleStroke(strokeIndex);
        }

        // ================ 数据层事件响应 ================

        private void OnStrokeSelectionChanged(int strokeIndex, bool isSelected)
        {
            UpdateStrokeVisual(strokeIndex, isSelected);
        }

        private void OnAnswerSubmitted(bool success, string answerCharacter, string message)
        {
            GameEvent.Send(EventDefine.Event_AnswerSubmitted, success, answerCharacter, message);
            if (success)
                ClearAllHighlights();
        }

        private void OnLevelCompleted(int levelIndex)
        {
            GameManager.Instance?.OnCorePlayLevelCompleted(levelIndex);
        }

        // ================ 视觉更新 ================

        private void UpdateStrokeVisual(int strokeIndex, bool isSelected)
        {
            if (_drawCharacter == null) return;

            var strokes = _drawCharacter.StrokeObjects;
            if (strokeIndex < 0 || strokeIndex >= strokes.Count) return;

            GameObject obj = strokes[strokeIndex];
            if (obj == null) return;

            if (isSelected)
            {
                _drawCharacter.SetStrokeColor(strokeIndex, _highlightColor);
                var pos = obj.transform.localPosition;
                pos.z = _highlightZOffset;
                obj.transform.localPosition = pos;
            }
            else
            {
                _drawCharacter.SetStrokeColor(strokeIndex, _defaultStrokeColor);
                var pos = obj.transform.localPosition;
                pos.z = 0;
                obj.transform.localPosition = pos;
            }
        }

        public void ClearAllHighlights()
        {
            if (_drawCharacter == null) return;
            _drawCharacter.ResetAllStrokeColors();

            var strokes = _drawCharacter.StrokeObjects;
            for (int i = 0; i < strokes.Count; i++)
            {
                if (strokes[i] != null)
                {
                    var pos = strokes[i].transform.localPosition;
                    pos.z = 0;
                    strokes[i].transform.localPosition = pos;
                }
            }
        }

        // ================ UI 回调 ================

        public void OnSubmitClicked()
        {
            _gamePlay?.SubmitAnswer();
        }

        public void OnNextLevelClicked()
        {
            (_gamePlay as CorePlayGamePlay)?.ClearSelection();
            ClearAllHighlights();
            GameManager.Instance?.LoadNextCorePlayLevel();
        }
    }
}