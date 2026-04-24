using UnityEngine;
using UnityEngine.UI;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeHealth))]
    public sealed class PrototypeFloatingHealthBar : MonoBehaviour
    {
        private const string CanvasName = "Prototype Floating Health Bars";
        private const string LayerName = "Bars";
        private const int SortingOrder = 90;
        private const float HiddenWhenFullThreshold = 0.999f;

        private static Canvas sharedCanvas;
        private static RectTransform sharedLayer;

        public enum Preset
        {
            Player,
            Enemy
        }

        [SerializeField]
        private Preset preset = Preset.Enemy;

        [SerializeField]
        private Vector3 worldOffset = new Vector3(0f, 1.85f, 0f);

        [SerializeField]
        private Vector2 size = new Vector2(46f, 5f);

        [SerializeField]
        private Color fillColor = new Color(1f, 0.23f, 0.16f, 0.88f);

        [SerializeField]
        private Color backgroundColor = new Color(0f, 0f, 0f, 0.58f);

        private PrototypeHealth health;
        private Camera targetCamera;
        private RectTransform barRoot;
        private RectTransform fillRect;
        private Image backgroundImage;
        private Image fillImage;

        public Preset CurrentPreset => preset;

        public static PrototypeFloatingHealthBar EnsureFor(PrototypeHealth health, Preset preset)
        {
            if (health == null)
            {
                return null;
            }

            if (!health.TryGetComponent<PrototypeFloatingHealthBar>(out var bar))
            {
                bar = health.gameObject.AddComponent<PrototypeFloatingHealthBar>();
            }

            bar.Configure(preset);
            return bar;
        }

        public void Configure(Preset newPreset)
        {
            preset = newPreset;
            ApplyPreset(newPreset);
            ApplyVisualStyle();
        }

        private void Awake()
        {
            health = GetComponent<PrototypeHealth>();
            ApplyPreset(preset);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureVisual();
            Refresh();
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            DestroyVisual();
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(8f, size.x);
            size.y = Mathf.Max(3f, size.y);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureVisual();
            Refresh();
        }

        private void ApplyPreset(Preset targetPreset)
        {
            if (targetPreset == Preset.Player)
            {
                worldOffset = new Vector3(0f, 2.35f, 0f);
                size = new Vector2(72f, 8f);
                fillColor = new Color(0.14f, 0.95f, 1f, 0.86f);
                backgroundColor = new Color(0f, 0f, 0f, 0.52f);
                return;
            }

            worldOffset = new Vector3(0f, 1.85f, 0f);
            size = new Vector2(46f, 5f);
            fillColor = new Color(1f, 0.23f, 0.16f, 0.88f);
            backgroundColor = new Color(0f, 0f, 0f, 0.58f);
        }

        private void EnsureVisual()
        {
            if (barRoot != null && fillRect != null && backgroundImage != null && fillImage != null)
            {
                return;
            }

            var layer = EnsureSharedLayer();
            if (layer == null)
            {
                return;
            }

            var rootObject = new GameObject($"{name} Health Bar", typeof(RectTransform));
            rootObject.transform.SetParent(layer, false);
            barRoot = rootObject.GetComponent<RectTransform>();
            barRoot.anchorMin = new Vector2(0.5f, 0.5f);
            barRoot.anchorMax = new Vector2(0.5f, 0.5f);
            barRoot.pivot = new Vector2(0.5f, 0.5f);
            barRoot.sizeDelta = size;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(barRoot, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(backgroundRect, false);
            fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillImage = fillObject.GetComponent<Image>();
            fillImage.raycastTarget = false;

            ApplyVisualStyle();
            SetVisible(false);
        }

        private static RectTransform EnsureSharedLayer()
        {
            if (sharedLayer != null)
            {
                return sharedLayer;
            }

            if (sharedCanvas == null)
            {
                var existingCanvasObject = GameObject.Find(CanvasName);
                sharedCanvas = existingCanvasObject != null ? existingCanvasObject.GetComponent<Canvas>() : null;
            }

            if (sharedCanvas == null)
            {
                var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                sharedCanvas = canvasObject.GetComponent<Canvas>();
                sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                sharedCanvas.sortingOrder = SortingOrder;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                sharedCanvas.sortingOrder = SortingOrder;
            }

            var layerTransform = sharedCanvas.transform.Find(LayerName) as RectTransform;
            if (layerTransform == null)
            {
                var layerObject = new GameObject(LayerName, typeof(RectTransform));
                layerObject.transform.SetParent(sharedCanvas.transform, false);
                layerTransform = layerObject.GetComponent<RectTransform>();
            }

            layerTransform.anchorMin = Vector2.zero;
            layerTransform.anchorMax = Vector2.one;
            layerTransform.pivot = new Vector2(0.5f, 0.5f);
            layerTransform.offsetMin = Vector2.zero;
            layerTransform.offsetMax = Vector2.zero;
            sharedLayer = layerTransform;
            return sharedLayer;
        }

        private void ApplyVisualStyle()
        {
            if (barRoot != null)
            {
                barRoot.sizeDelta = size;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
            }

            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }
        }

        private void Refresh()
        {
            health = health != null ? health : GetComponent<PrototypeHealth>();
            if (health == null || barRoot == null || fillRect == null)
            {
                SetVisible(false);
                return;
            }

            var normalizedHealth = health.NormalizedHealth;
            if (health.IsDead || normalizedHealth <= 0f || normalizedHealth >= HiddenWhenFullThreshold)
            {
                SetVisible(false);
                return;
            }

            targetCamera = targetCamera != null ? targetCamera : Camera.main;
            if (targetCamera == null || sharedLayer == null)
            {
                SetVisible(false);
                return;
            }

            var worldPosition = transform.position + worldOffset;
            var viewportPosition = targetCamera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z <= 0f
                || viewportPosition.x < 0f
                || viewportPosition.x > 1f
                || viewportPosition.y < 0f
                || viewportPosition.y > 1f)
            {
                SetVisible(false);
                return;
            }

            var screenPosition = targetCamera.WorldToScreenPoint(worldPosition);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(sharedLayer, screenPosition, null, out var localPosition))
            {
                SetVisible(false);
                return;
            }

            barRoot.anchoredPosition = localPosition;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(normalizedHealth), 1f);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (barRoot != null && barRoot.gameObject.activeSelf != visible)
            {
                barRoot.gameObject.SetActive(visible);
            }
        }

        private void DestroyVisual()
        {
            if (barRoot == null)
            {
                return;
            }

            var rootObject = barRoot.gameObject;
            barRoot = null;
            fillRect = null;
            backgroundImage = null;
            fillImage = null;

            if (Application.isPlaying)
            {
                Destroy(rootObject);
            }
            else
            {
                DestroyImmediate(rootObject);
            }
        }
    }
}
