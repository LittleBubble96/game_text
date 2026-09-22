using System.Collections.Generic;
using System.Linq;
using GameLogic.GamePlay.CorePlay;
using GameLogic.GamePlay.CorePlay.View;
using GameLogic.Localization;
using RTLTMPro;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.System, location: "UIGuide")]
    public class UIGuide : UIWindow
    {
        private enum Step { Waiting, Stroke, Submit, Finish }
        private static UIGuide _active;
        public static bool BlocksGameplayInput => _active != null && _active._step != Step.Finish;
        private RectTransform _handRoot, _tipAnchor, _submit;
        private RTLTextMeshPro _description;
        private CorePlayGamePlay _game;
        private CorePlayView _view;
        private Step _step;
        private int _answerIndex, _strokeOffset;
        private List<int> _selection;
        private Image _mask;
        private Canvas _overlayCanvas, _submitCanvas;
        private bool _createdSubmitCanvas, _submitCanvasEnabled, _submitOverrideSorting;
        private int _submitSortingLayer, _submitSortingOrder;
        private Image _input;
        private RawImage _highlight;
        private Camera _camera, _sourceCamera;
        private RenderTexture _texture;
        private GameObject _stroke;
        private int _originalLayer;
        private float _transition, _finishTime, _clickAfter;
        private string _pendingText;
        private bool _textChanged, _closed;
        private Vector3 _textScale;
        private Vector2 _handPosition;
        private bool _handPositioned;
        private Vector3 _strokePoint;

        protected override void ScriptGenerator()
        {
            _handRoot = FindChildComponent<RectTransform>("handleRoot");
            _tipAnchor = _handRoot.Find("TipAnchor") as RectTransform;
            _description = FindChildComponent<RTLTextMeshPro>("des");
            _textScale = _description.transform.localScale;
            foreach (var graphic in transform.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            // 遮罩统一由引导控制，避免与预制体背景叠加。
            var background = gameObject.GetComponent<Image>();
            if (background != null) background.enabled = false;
            _mask = CreateGraphic<Image>("GuideMask");
            _mask.color = new Color(0, 0, 0, .65f);
            _highlight = CreateGraphic<RawImage>("GuideHighlight");
            _input = CreateGraphic<Image>("GuideInput");
            _input.color = Color.clear;
            _input.raycastTarget = true;
            var trigger = _input.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data => HandleClick((PointerEventData)data));
            trigger.triggers.Add(entry);
            // 透明输入层和手指始终位于抬高的按钮之上，统一处理引导点击。
            var overlay = new GameObject("GuideOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            overlay.layer = gameObject.layer;
            var overlayRect = (RectTransform)overlay.transform;
            overlayRect.SetParent(transform, false);
            overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            _overlayCanvas = overlay.GetComponent<Canvas>();
            _input.transform.SetParent(overlayRect, false);
            _handRoot.SetParent(overlayRect, false);
            _description.transform.SetParent(overlayRect, false);
            UpdateSorting();
        }
        private T CreateGraphic<T>(string name) where T : Graphic
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            obj.layer = gameObject.layer;
            obj.transform.SetParent(transform, false);
            var graphic = obj.AddComponent<T>();
            graphic.raycastTarget = false;
            SetRect(graphic.rectTransform, rectTransform.rect);
            return graphic;
        }
        // 独立控制文案过渡，不让旧 UIFade 动画覆盖遮罩和输入状态。
        protected override void OnInAnimation() { OnInAnimationComplete(); }
        protected override void OnOutAnimation() { OnOutAnimationComplete(); }
        protected override void OnRefresh()
        {
            Cleanup(); _closed = false;
            var animation = gameObject.GetComponent<Animation>();
            if (animation != null)
            {
                animation.Stop();
                animation.Play("UIFadeIn");
            }
            var group = gameObject.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1;
            if (Canvas.worldCamera == null) Canvas.worldCamera = GameModule.UI.UICamera;
            _description.text = "";
            var handAnimation = _handRoot.GetComponent<Animation>();
            if (handAnimation != null) { handAnimation.wrapMode = WrapMode.Loop; handAnimation.Play(); }
            _game = UserDatas.Length > 0 ? UserDatas[0] as CorePlayGamePlay : null;
            _view = UserDatas.Length > 1 ? UserDatas[1] as CorePlayView : null;
            _submit = UserDatas.Length > 2 ? UserDatas[2] as RectTransform : null;
            if (_game == null || _view == null || _submit == null || _game != GameManager.Instance.CurrentGamePlay ||
                _game.CurrentLevelId != 1 || GameManager.Instance.FirstLevelGuideCompleted)
            { CloseGuide(); return; }
            var answers = _game.CurrentLevelData.answers;
            if (answers == null || answers.Count < 2 || !_view.GuideReady || Camera.main == null)
            { CloseGuide(); return; }
            for (int i = 0; i < 2; i++)
            {
                if (answers[i].strokeSets == null || answers[i].strokeSets.Count == 0 ||
                    answers[i].strokeSets[0].strokeIndices == null || answers[i].strokeSets[0].strokeIndices.Count == 0 ||
                    answers[i].strokeSets[0].strokeIndices.Any(s => s < 0 || s >= _view.GuideCharacter.StrokeObjects.Count))
                { Log.Warning("[UIGuide] 前两个答案缺少有效笔画组合，跳过引导。"); CloseGuide(); return; }
            }
            _active = this; _step = Step.Waiting;
            _game.ClearSelection();
            _view.ClearAllHighlights();
            _game.OnAnswerSubmitted += OnSubmitted;
            _answerIndex = 0; _handPositioned = false;
            _sourceCamera = Camera.main;
            int layer = LayerMask.NameToLayer("GuideHighlight");
            if (layer < 0) { Log.Error("[UIGuide] 缺少 GuideHighlight Layer。"); CloseGuide(); return; }
            _camera = new GameObject("GuideHighlightCamera").AddComponent<Camera>();
            _camera.CopyFrom(_sourceCamera); _camera.tag = "Untagged";
            _camera.cullingMask = 1 << layer;
            _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.clear;
            _camera.enabled = false; _camera.allowHDR = false; _camera.allowMSAA = false;
            BeginAnswer();
        }
        private void BeginAnswer()
        {
            var found = _game.GetFoundAnswerCharacters();
            while (_answerIndex < 2 && found.Contains(_game.CurrentLevelData.answers[_answerIndex].answerCharacter)) _answerIndex++;
            if (_answerIndex >= 2) { Finish(); return; }
            _selection = _game.CurrentLevelData.answers[_answerIndex].strokeSets[0].strokeIndices.Distinct().ToList();
            _strokeOffset = 0; ShowStroke();
        }
        private void ShowStroke()
        {
            RestoreSubmitSorting();
            RestoreStroke(); _step = Step.Stroke;
            _stroke = _view.GuideCharacter.StrokeObjects[_selection[_strokeOffset]];
            _originalLayer = _stroke.layer;
            _stroke.layer = LayerMask.NameToLayer("GuideHighlight");
            _strokePoint = StrokePoint();
            SetText(_strokeOffset > 0 ? LanguageKey.guide_step1 : _answerIndex == 0 ? LanguageKey.guide_step2 : LanguageKey.guide_step3);
        }
        private void HandleClick(PointerEventData data)
        {
            if (_closed || Time.unscaledTime < _clickAfter || UIInteractionLock.IsLocked) return;
            if (_step == Step.Stroke && _stroke != null)
            {
                var collider = _stroke.GetComponent<Collider2D>();
                var ray = _sourceCamera.ScreenPointToRay(data.position);
                var plane = new Plane(Vector3.forward, _stroke.transform.position);
                if (!plane.Raycast(ray, out float distance)) return;
                Vector2 world = ray.GetPoint(distance);
                if (collider == null || !collider.OverlapPoint(world)) return;
                _game.ToggleStroke(_selection[_strokeOffset]);
                AudioSystem.Instance.PlayAudio(AudioDefine.clickCharacter_SFX);
                _strokeOffset++;
                if (_strokeOffset < _selection.Count) ShowStroke();
                else
                {
                    RestoreStroke(); _step = Step.Submit;
                    RaiseSubmit();
                    SetText(_answerIndex == 0 ? LanguageKey.guide_step4 : LanguageKey.guide_step5);
                }
            }
            else if (_step == Step.Submit && RectTransformUtility.RectangleContainsScreenPoint(_submit, data.position, GetCanvasCamera(_submit)))
            {
                _clickAfter = Time.unscaledTime + .25f;
                AudioSystem.Instance.PlayAudio(AudioDefine.btnClick_SFX, 1f);
                _game.SubmitAnswer();
            }
        }
        private void OnSubmitted(bool success, string character, string message)
        {
            if (_step != Step.Submit) return;
            if (!success || character != _game.CurrentLevelData.answers[_answerIndex].answerCharacter)
            { _game.ClearSelection(); _strokeOffset = 0; ShowStroke(); return; }
            _answerIndex++; BeginAnswer();
        }
        private void Finish()
        {
            RestoreSubmitSorting();
            RestoreStroke(); _step = Step.Finish; _finishTime = Time.unscaledTime;
            _input.raycastTarget = false; _handRoot.gameObject.SetActive(false); _highlight.enabled = false;
            SetText(LanguageKey.guide_step6);
            GameManager.Instance.CompleteFirstLevelGuide();
        }
        private void SetText(string languageKey)
        {
            _pendingText = LocalizationHelper.GetLocalText(languageKey); _transition = 0; _textChanged = false;
            _clickAfter = Time.unscaledTime + .25f;
        }
        protected override void OnUpdate()
        {
            if (_closed) return;
            if (_game == null || _view == null || _submit == null || _sourceCamera == null || !_view.gameObject.activeInHierarchy ||
                _game != GameManager.Instance.CurrentGamePlay || _game.CurrentLevelId != 1)
            { CloseGuide(); return; }
            _transition += Time.unscaledDeltaTime;
            if (_transition >= .1f && !_textChanged) { _description.text = _pendingText; _textChanged = true; }
            _description.alpha = _transition < .1f ? 1 - _transition / .1f : Mathf.Clamp01((_transition - .1f) / .15f);
            _description.transform.localScale = _textScale * Mathf.Lerp(.94f, 1, Mathf.Clamp01((_transition - .1f) / .15f));
            SetRect(_input.rectTransform, rectTransform.rect);
            SetRect(_mask.rectTransform, rectTransform.rect);
            UpdateSorting();
            if (_step == Step.Finish)
            {
                float alpha = .65f * (1 - Mathf.Clamp01((Time.unscaledTime - _finishTime) / .35f));
                _mask.color = new Color(0, 0, 0, alpha);
                if (Time.unscaledTime - _finishTime > 2.5f) CloseGuide();
                return;
            }
            _input.raycastTarget = true; _handRoot.gameObject.SetActive(true);
            _mask.color = new Color(0, 0, 0, .65f);
            Vector2 screen;
            if (_step == Step.Stroke && _stroke != null)
            {
                RenderHighlight(); screen = _sourceCamera.WorldToScreenPoint(_strokePoint);
            }
            else if (_step == Step.Submit)
            {
                _highlight.enabled = false;
                screen = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(_submit), _submit.TransformPoint(_submit.rect.center));
            }
            else return;
            Vector2 tipOffset = _tipAnchor != null ? (Vector2)transform.InverseTransformVector(_tipAnchor.position - _handRoot.position) : new Vector2(-48, 72);
            Vector2 target = ToLocal(screen) - tipOffset;
            _handPosition = _handPositioned ? Vector2.Lerp(_handPosition, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime)) : target;
            _handPositioned = true;
            _handRoot.localPosition = new Vector3(_handPosition.x, _handPosition.y, 0);
        }
        private void RenderHighlight()
        {
            int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null) { _camera.targetTexture = null; _texture.Release(); Object.Destroy(_texture); }
                _texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
                _texture.Create(); _camera.targetTexture = _texture; _highlight.texture = _texture;
            }
            _camera.transform.SetPositionAndRotation(_sourceCamera.transform.position, _sourceCamera.transform.rotation);
            _camera.projectionMatrix = _sourceCamera.projectionMatrix;
            _camera.Render(); _highlight.enabled = true;
            SetRect(_highlight.rectTransform, rectTransform.rect);
        }
        private Vector3 StrokePoint()
        {
            var mesh = _stroke.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            float largest = -1; Vector3 point = mesh.bounds.center;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                float area = Vector3.Cross(b - a, c - a).sqrMagnitude;
                if (area > largest) { largest = area; point = (a + b + c) / 3; }
            }
            return _stroke.transform.TransformPoint(point);
        }
        private static Camera GetCanvasCamera(Transform target)
        {
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : GameModule.UI.UICamera;
        }
        private Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, GetCanvasCamera(transform), out var point);
            return point;
        }
        private static void SetRect(RectTransform target, Rect rect)
        {
            target.anchorMin = target.anchorMax = new Vector2(.5f, .5f); target.pivot = new Vector2(.5f, .5f);
            target.anchoredPosition = rect.center; target.sizeDelta = rect.size;
        }
        private void RestoreStroke()
        {
            if (_stroke != null) _stroke.layer = _originalLayer;
            _stroke = null;
        }
        private void RaiseSubmit()
        {
            if (_submitCanvas == null)
            {
                _submitCanvas = _submit.GetComponent<Canvas>();
                _createdSubmitCanvas = _submitCanvas == null;
                if (_createdSubmitCanvas) _submitCanvas = _submit.gameObject.AddComponent<Canvas>();
                _submitCanvasEnabled = _submitCanvas.enabled;
                _submitOverrideSorting = _submitCanvas.overrideSorting;
                _submitSortingLayer = _submitCanvas.sortingLayerID;
                _submitSortingOrder = _submitCanvas.sortingOrder;
            }
            _submitCanvas.enabled = true;
            UpdateSorting();
        }
        private void UpdateSorting()
        {
            // 整张遮罩 < 提交按钮 < 透明输入层、手指和文案。
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.worldCamera = GetCanvasCamera(transform);
            _overlayCanvas.sortingLayerID = Canvas.sortingLayerID;
            _overlayCanvas.sortingOrder = Canvas.sortingOrder + 2;
            if (_step != Step.Submit || _submitCanvas == null) return;
            _submitCanvas.overrideSorting = true;
            _submitCanvas.sortingLayerID = Canvas.sortingLayerID;
            _submitCanvas.sortingOrder = Canvas.sortingOrder + 1;
        }
        private void RestoreSubmitSorting()
        {
            if (_submitCanvas == null) return;
            _submitCanvas.sortingLayerID = _submitSortingLayer;
            _submitCanvas.sortingOrder = _submitSortingOrder;
            _submitCanvas.overrideSorting = _submitOverrideSorting;
            _submitCanvas.enabled = _submitCanvasEnabled;
        }
        private void Cleanup()
        {
            if (_game != null) _game.OnAnswerSubmitted -= OnSubmitted;
            RestoreSubmitSorting();
            if (_createdSubmitCanvas && _submitCanvas != null) Object.Destroy(_submitCanvas);
            _submitCanvas = null; _createdSubmitCanvas = false;
            RestoreStroke();
            if (_active == this) _active = null;
            if (_camera != null) { _camera.targetTexture = null; Object.Destroy(_camera.gameObject); }
            if (_texture != null) { _texture.Release(); Object.Destroy(_texture); }
            _camera = null; _texture = null;
        }
        private void CloseGuide()
        {
            if (_closed) return;
            _closed = true; Cleanup(); GameModule.UI.CloseUI<UIGuide>();
        }
        protected override void OnDestroy() { Cleanup(); base.OnDestroy(); }
        protected override void OnSetVisible(bool visible)
        {
            if (!visible && _active == this) CloseGuide();
        }
    }
}
