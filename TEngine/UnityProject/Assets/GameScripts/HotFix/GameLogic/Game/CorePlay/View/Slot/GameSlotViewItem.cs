using System.Collections.Generic;
using DG.Tweening;
using RTLTMPro;
using UnityEngine;

namespace GameLogic.GamePlay.CorePlay.View
{
    /// <summary>整个插槽栏共用，按同时飞行数量扩容，视图销毁时统一释放材质。</summary>
    internal sealed class FlightStrokePool : System.IDisposable
    {
        internal sealed class Stroke
        {
            public GameObject Object;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material Material;
        }

        internal sealed class Flight
        {
            public GameObject Root;
            public readonly List<Stroke> Strokes = new List<Stroke>();
            public bool InUse;
        }

        private readonly GameObject _parkingRoot;
        private readonly List<Flight> _flights = new List<Flight>();
        private bool _disposed;

        public FlightStrokePool(Transform parent)
        {
            _parkingRoot = new GameObject("FlightStrokePool");
            _parkingRoot.transform.SetParent(parent, false);
            _parkingRoot.SetActive(false);
        }

        public Flight Acquire(Transform parent, IReadOnlyList<GameObject> sources, Vector3 center,
            int sortingLayer, int sortingOrder)
        {
            Flight flight = null;
            foreach (var candidate in _flights)
                if (!candidate.InUse)
                {
                    flight = candidate;
                    break;
                }
            if (flight == null)
            {
                flight = new Flight { Root = new GameObject("SubmittedStrokes") };
                flight.Root.SetActive(false);
                _flights.Add(flight);
            }
            flight.InUse = true;
            var root = flight.Root.transform;
            root.SetParent(null, false);
            root.SetPositionAndRotation(center, Quaternion.identity);
            root.localScale = Vector3.one;
            root.SetParent(parent, true);
            int used = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null) continue;
                var renderer = source.GetComponent<MeshRenderer>();
                var filter = source.GetComponent<MeshFilter>();
                if (renderer == null || renderer.sharedMaterial == null || filter == null || filter.sharedMesh == null)
                    continue;
                Stroke stroke;
                if (used == flight.Strokes.Count)
                {
                    var obj = new GameObject("FlyingStroke");
                    obj.SetActive(false);
                    stroke = new Stroke
                    {
                        Object = obj,
                        Filter = obj.AddComponent<MeshFilter>(),
                        Renderer = obj.AddComponent<MeshRenderer>(),
                        Material = new Material(renderer.sharedMaterial)
                    };
                    stroke.Renderer.sharedMaterial = stroke.Material;
                    flight.Strokes.Add(stroke);
                }
                else
                {
                    stroke = flight.Strokes[used];
                    // 独立材质快照，原笔画清除选中色时不会改变飞行中的颜色。
                    if (stroke.Material.shader != renderer.sharedMaterial.shader)
                        stroke.Material.shader = renderer.sharedMaterial.shader;
                    stroke.Material.CopyPropertiesFromMaterial(renderer.sharedMaterial);
                }
                used++;
                var tf = stroke.Object.transform;
                tf.SetParent(null, false);
                tf.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                tf.localScale = source.transform.lossyScale;
                tf.SetParent(root, true);
                stroke.Object.layer = source.layer;
                stroke.Filter.sharedMesh = filter.sharedMesh;
                stroke.Renderer.sortingLayerID = sortingLayer;
                stroke.Renderer.sortingOrder = sortingOrder;
                stroke.Object.SetActive(true);
            }
            flight.Root.SetActive(true);
            return flight;
        }

        public void Release(Flight flight)
        {
            if (_disposed || !flight.InUse) return;
            flight.InUse = false;
            flight.Root.SetActive(false);
            foreach (var stroke in flight.Strokes)
            {
                stroke.Object.SetActive(false);
                // 网格由 DrawCharacter 持有，归还时断开引用，不销毁共享资源。
                stroke.Filter.sharedMesh = null;
            }
            flight.Root.transform.SetParent(_parkingRoot.transform, false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var flight in _flights)
            {
                if (flight.Root != null) flight.Root.SetActive(false);
                foreach (var stroke in flight.Strokes) Object.Destroy(stroke.Material);
                Object.Destroy(flight.Root);
            }
            _flights.Clear();
            Object.Destroy(_parkingRoot);
        }
    }

    public class GameSlotViewItem : MonoBehaviour
    {
        public const string ResPath = "GameSlotViewItem";

        [SerializeField] private SpriteRenderer bg;
        [SerializeField] private RTLTextMeshPro3D content;
        [SerializeField] private RTLTextMeshPro3D contentTone;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private SpriteRenderer contentBg;
        [SerializeField] private GameObject root;

        public bool IsFilled { get; private set; }

        private Sequence _sequence;
        private Sequence _putSequence;
        private GameObject _flyingStrokes;
        private FlightStrokePool _flightPool;
        private FlightStrokePool.Flight _flight;
        private Vector3 _backgroundScale;
        private Vector3 _textScale;
        private Vector3 _toneScale;

        private void Awake()
        {
            _backgroundScale = contentBg.transform.localScale;
            _textScale = content.transform.localScale;
            _toneScale = contentTone.transform.localScale;
        }

        private void ClearFlight()
        {
            if (_flight != null) _flightPool.Release(_flight);
            _flight = null;
            _flightPool = null;
            _flyingStrokes = null;
        }

        private void ResetVisuals()
        {
            root.transform.localScale = Vector3.one;
            contentRoot.localPosition = Vector3.zero;
            contentBg.transform.localScale = _backgroundScale;
            content.transform.localScale = _textScale;
            contentTone.transform.localScale = _toneScale;
            bg.color = WithAlpha(bg.color, 1f);
            contentBg.color = WithAlpha(contentBg.color, 1f);
            content.color = WithAlpha(content.color, 1f);
            contentTone.color = WithAlpha(contentTone.color, 1f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void StopAnimations()
        {
            _sequence?.Kill();
            _putSequence?.Kill();
            _sequence = null;
            _putSequence = null;
            ClearFlight();
        }

        private void OnDisable() => StopAnimations();
        private void OnDestroy() => StopAnimations();

        internal void CancelFlight()
        {
            StopAnimations();
            ResetVisuals();
            contentRoot.gameObject.SetActive(IsFilled);
        }

        /// <summary>显示空状态：只显示背景，不显示内容</summary>
        public void ShowEmptyState()
        {
            StopAnimations();
            ResetVisuals();
            IsFilled = false;
            contentRoot.gameObject.SetActive(false);
            if (content != null) content.text = "";
        }

        /// <summary>设置内容并播放放入动画</summary>
        internal void SetContentAndPlay(string text, IReadOnlyList<GameObject> strokes, FlightStrokePool pool)
        {
            StopAnimations();
            ResetVisuals();
            IsFilled = true;
            contentRoot.gameObject.SetActive(true);
            if (content != null) content.text = text;
            if (contentTone != null)
            {
                contentTone.text = GetTone(text);
            }
            _flightPool = pool;
            PlayPutAnimation(strokes);
        }

        /// <summary>直接设置内容（无动画，用于恢复存档）</summary>
        public void SetContentImmediate(string text)
        {
            StopAnimations();
            ResetVisuals();
            IsFilled = true;
            contentRoot.gameObject.SetActive(true);
            if (content != null)
            {
                content.text = text;
                content.color = new Color(content.color.r, content.color.g, content.color.b, 1f);
            }

            if (contentTone!=null)
            {
                contentTone.text = GetTone(text);
                contentTone.color = new Color(contentTone.color.r, contentTone.color.g, contentTone.color.b, 1f);
            }
        }

        // 对象和独立材质从共享池借用；网格只读共享，切关重绘前取消飞行。
        private float CreateFlight(IReadOnlyList<GameObject> strokes)
        {
            if (_flightPool == null || strokes == null || strokes.Count == 0) return 0f;
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var stroke in strokes)
            {
                if (stroke == null) continue;
                var renderer = stroke.GetComponent<MeshRenderer>();
                var filter = stroke.GetComponent<MeshFilter>();
                if (renderer == null || renderer.sharedMaterial == null || filter == null || filter.sharedMesh == null) continue;
                if (!hasBounds) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                hasBounds = true;
            }
            if (!hasBounds) return 0f;

            _flight = _flightPool.Acquire(transform, strokes, bounds.center, bg.sortingLayerID,
                Mathf.Max(bg.sortingOrder, contentBg.sortingOrder) + 10);
            _flyingStrokes = _flight.Root;
            float targetSize = Mathf.Max(bg.bounds.size.x, bg.bounds.size.y) * 0.65f;
            return Mathf.Min(1f, targetSize / Mathf.Max(0.001f, Mathf.Max(bounds.size.x, bounds.size.y)));
        }

        private void PlayPutAnimation(IReadOnlyList<GameObject> strokes)
        {
            float flightScale = CreateFlight(strokes);
            contentRoot.gameObject.SetActive(false);
            Sequence sequence = _putSequence = DOTween.Sequence();
            if (_flyingStrokes != null)
            {
                var flying = _flyingStrokes.transform;
                Vector3 start = flying.position;
                Vector3 initialScale = flying.localScale;
                // 每帧取落点，窗口尺寸变化重新布局时仍能落入当前插槽。
                sequence.Append(DOTween.To(() => 0f, progress =>
                {
                    Vector3 target = contentRoot.position;
                    target.z = start.z;
                    flying.position = Vector3.Lerp(start, target, progress)
                        + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.35f);
                    flying.localScale = initialScale * Mathf.Lerp(1f, flightScale, progress);
                }, 1f, 0.32f).SetEase(Ease.InOutCubic));
            }
            sequence.AppendCallback(() =>
            {
                ClearFlight();
                contentRoot.gameObject.SetActive(true);
                contentBg.transform.localScale = _backgroundScale * 0.3f;
                contentBg.color = WithAlpha(contentBg.color, 0f);
                content.transform.localScale = _textScale * 0.8f;
                contentTone.transform.localScale = _toneScale * 0.8f;
            });
            sequence.Append(root.transform.DOScale(new Vector3(1.08f, 0.9f, 1f), 0.07f).SetEase(Ease.OutQuad));
            sequence.Join(contentBg.transform.DOScale(_backgroundScale, 0.24f).SetEase(Ease.OutBack));
            sequence.Join(contentBg.DOFade(1f, 0.18f));
            sequence.Join(content.transform.DOScale(_textScale, 0.2f).SetEase(Ease.OutBack));
            sequence.Join(contentTone.transform.DOScale(_toneScale, 0.2f).SetEase(Ease.OutBack));
            sequence.Insert((flightScale > 0f ? 0.32f : 0f) + 0.07f,
                root.transform.DOScale(Vector3.one, 0.18f).SetEase(Ease.OutBack));
            sequence.OnComplete(ResetVisuals);
        }

        private string GetTone(string character)
        {
            Dictionary<string, string> dic = GameManager.Instance.LevelConfig.CharacterToToneMap;
            return dic.GetValueOrDefault(character, "");
        }

        public void PlayEnterAnim()
        {
            root.transform.localScale = Vector3.one * 0.5f;
            bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0);
            content.color = new Color(content.color.r, content.color.g, content.color.b, 0);
            contentTone.color = new Color(content.color.r, content.color.g, content.color.b, 0);
            contentBg.color = new Color(contentBg.color.r, contentBg.color.g, contentBg.color.b, 0);
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.Append(content.DOColor(new Color(content.color.r, content.color.g, content.color.b, 1f), 0.2f).SetEase(Ease.OutCubic));
            _sequence.Join(contentTone.DOColor(new Color(content.color.r, content.color.g, content.color.b, 1f), 0.2f).SetEase(Ease.OutCubic));
            _sequence.Join(contentBg.DOColor(new Color(contentBg.color.r, contentBg.color.g, contentBg.color.b, 1f), 0.2f).SetEase(Ease.OutCubic));
            _sequence.Join(bg.DOColor(new Color(bg.color.r, bg.color.g, bg.color.b, 1f), 0.2f).SetEase(Ease.OutCubic));
            _sequence.Join(root.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutCubic));
            _sequence.OnKill(() =>
            {
                root.transform.localScale = Vector3.one;
                bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 1);
                content.color = new Color(content.color.r, content.color.g, content.color.b, 1);
                contentTone.color = new Color(content.color.r, content.color.g, content.color.b, 1);
                contentBg.color = new Color(contentBg.color.r, contentBg.color.g, contentBg.color.b, 1);
            });
        }

        public void PlayExitAnim()
        {
            StopAnimations();
            ResetVisuals();
            bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 1);
            content.color = new Color(content.color.r, content.color.g, content.color.b, 1);
            contentTone.color = new Color(content.color.r, content.color.g, content.color.b, 1);
            contentBg.color = new Color(contentBg.color.r, contentBg.color.g, contentBg.color.b, 1);
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.Append(content.DOColor(new Color(content.color.r, content.color.g, content.color.b, 0f), 0.2f).SetEase(Ease.OutCubic));
            _sequence.Join(contentTone.DOColor(new Color(content.color.r, content.color.g, content.color.b, 0f), 0.2f).SetEase(Ease.OutCubic));
            _sequence.Join(contentBg.DOColor(new Color(contentBg.color.r, contentBg.color.g, contentBg.color.b, 0f), 0.2f).SetEase(Ease.OutCubic));
            _sequence.Join(bg.DOColor(new Color(bg.color.r, bg.color.g, bg.color.b, 0f), 0.2f).SetEase(Ease.OutCubic));
        }
    }
}
