using System.Collections.Generic;
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
        
        private Color _highlightColor = Color.black;
        private Color _defaultStrokeColor = Color.white * 0.5f;
        private float _highlightZOffset = -1f;

        // ================ 提示闪烁 ================
        private Color _tipHighlightColor = new Color(1f, 0.4f, 0.1f, 1f);
        private Color _tipFadeColor = new Color(1f, 0.4f, 0.1f, 0.2f);
        private float _tipBlinkSpeed = 5f;
        private float _tipBlinkDuration = 2f;
        private List<int> _tipHighlightStrokes;
        private float _tipBlinkTimer;
        private bool _tipBlinkActive;

        // ================ 内部状态 ================

        private IGamePlay _gamePlay;
        private LevelDataConfigParse _levelConfig;
        private DrawCharacter _drawCharacter;
        private StrokeInputHandler _strokeInputHandler;
        private bool _isInitialized;

        private GameViewRoot _gameViewRoot;
        private GameSlotView _gameSlotView;

        // ================ 属性 ================


        // ================ 动态初始化 ================

        public void OnCreate()
        {
             GameObject gameViewRoot = GameModule.Resource.LoadGameObject("GameViewRoot" , transform);
             if (gameViewRoot)
             {
                 _gameViewRoot = gameViewRoot.GetComponent<GameViewRoot>();
                 _gameViewRoot?.Init();
             }
             CreateSlotView();
        }

        private void CreateSlotView()
        {
            if (_gameViewRoot == null || _gameViewRoot.SlotRoot == null) return;
            _gameSlotView = new GameSlotView();
            _gameSlotView.OnCreate(_gameViewRoot.SlotRoot);
        }

        /// <summary>初始化视图，绑定数据层（通过 IGamePlay 接口）</summary>
        public void Initialize(IGamePlay gamePlay, LevelDataConfigParse levelConfig)
        {
            // 如果已初始化，先反注册所有事件，避免重复注册
            if (_isInitialized)
            {
                RemoveEventListeners();
            }

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

            // 绑定提示道具事件
            GameEvent.AddEventListener<List<int>>(EventDefine.Event_PropTipHighlight, OnPropTipHighlight);
            GameEvent.AddEventListener(EventDefine.Event_PropTipClearHighlight, OnPropTipClearHighlight);

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
            dcGo.transform.SetParent(_gameViewRoot.CharacterRoot);
            _drawCharacter = dcGo.AddComponent<DrawCharacter>();
            _drawCharacter.DefaultStrokeColor = _defaultStrokeColor;
            dcGo.transform.localPosition = Vector3.zero;
            dcGo.transform.localScale = Vector3.one;
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
            RemoveEventListeners();

            _gameSlotView?.OnDestroy();
            _gameSlotView = null;
        }

        /// <summary>移除所有事件监听，防止重复注册或泄漏</summary>
        private void RemoveEventListeners()
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

            GameEvent.RemoveEventListener<List<int>>(EventDefine.Event_PropTipHighlight, OnPropTipHighlight);
            GameEvent.RemoveEventListener(EventDefine.Event_PropTipClearHighlight, OnPropTipClearHighlight);
        }

        // ================ 关卡渲染 ================

        private void OnLevelLoaded(TextLevelData levelData)
        {
            RenderLevel(levelData);

            // 初始化 slot 视图（答案数量）
            int requiredCount = (_gamePlay as CorePlayGamePlay)?.GetRequiredAnswerCount() ?? levelData.answers.Count;
            _gameSlotView?.InitSlotView(requiredCount);

            // 恢复已找到的答案
            if (_gamePlay is CorePlayGamePlay corePlay)
            {
                var foundAnswers = corePlay.GetFoundAnswerCharacters();
                if (foundAnswers != null && foundAnswers.Count > 0)
                {
                    _gameSlotView?.RestoreAnswers(foundAnswers);
                }
            }
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
            // 用户选中任意笔画后，清除提示闪烁效果
            if (isSelected)
                ClearTipBlink();

            UpdateStrokeVisual(strokeIndex, isSelected);
        }

        private void OnAnswerSubmitted(bool success, string answerCharacter, string message)
        {
            GameEvent.Send(EventDefine.Event_AnswerSubmitted, success, answerCharacter, message);
            if (success)
                ClearAllHighlights();
        }

        private void OnLevelCompleted(int levelId)
        {
            ClearAllHighlights();
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

            // 同时清除提示闪烁
            ClearTipBlink();
        }

        // ================ 提示闪烁效果 ================

        private void Update()
        {
            if (!_tipBlinkActive || _drawCharacter == null) return;

            _tipBlinkTimer -= Time.deltaTime;

            // 闪烁2秒后自动消失
            if (_tipBlinkTimer <= 0)
            {
                ClearTipBlink();
                return;
            }

            // 闪烁效果：在亮色和暗色之间切换
            float t = Mathf.PingPong(Time.time * _tipBlinkSpeed, 1f);
            Color blinkColor = Color.Lerp(_tipFadeColor, _tipHighlightColor, t);

            if (_tipHighlightStrokes != null)
            {
                foreach (int idx in _tipHighlightStrokes)
                {
                    _drawCharacter.SetStrokeColor(idx, blinkColor);
                }
            }
        }

        /// <summary>开始提示高亮闪烁</summary>
        private void OnPropTipHighlight(List<int> strokeIndices)
        {
            if (strokeIndices == null || strokeIndices.Count == 0 || _drawCharacter == null) return;

            // 先清除之前的闪烁
            ClearTipBlink();

            _tipHighlightStrokes = new List<int>(strokeIndices);
            _tipBlinkTimer = _tipBlinkDuration;
            _tipBlinkActive = true;
        }

        /// <summary>清除提示高亮闪烁</summary>
        private void OnPropTipClearHighlight()
        {
            ClearTipBlink();
        }

        private void ClearTipBlink()
        {
            _tipBlinkActive = false;
            _tipBlinkTimer = 0;

            // 恢复提示笔画为默认颜色
            if (_tipHighlightStrokes != null && _drawCharacter != null)
            {
                foreach (int idx in _tipHighlightStrokes)
                {
                    _drawCharacter.SetStrokeColor(idx, _defaultStrokeColor);
                }
            }
            _tipHighlightStrokes = null;
        }

        // ================ 动画接口 ================

        /// <summary>进入游戏动画：背景淡入</summary>
        public void OnEnterGameAnim()
        {
            _gameViewRoot?.OnEnterGameAnim();
        }

        /// <summary>退出游戏动画：背景淡出 + 销毁 DrawCharacter</summary>
        public void OnEndGameAnim()
        {
            _gameViewRoot?.OnEndGameAnim();
            if (_drawCharacter != null)
            {
                Destroy(_drawCharacter.gameObject);
                _drawCharacter = null;
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