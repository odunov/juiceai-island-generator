using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeExpeditionRunHud : MonoBehaviour
    {
        [SerializeField]
        private PrototypeExpeditionRunController source;

        [SerializeField]
        private Vector2 anchorOffset = new Vector2(18f, -118f);

        [SerializeField]
        private Vector2 panelSize = new Vector2(240f, 124f);

        [Header("Resource Arrows")]
        [SerializeField]
        [Min(0f)]
        private float resourceArrowDistanceFromPlayer = 78f;

        [SerializeField]
        [Min(0f)]
        private float resourceArrowScreenPadding = 36f;

        [SerializeField]
        [Min(0f)]
        private float resourceArrowWorldHeight = 0.8f;

        [SerializeField]
        private Color resourceArrowColor = new Color(0.35f, 0.95f, 1f, 0.78f);

        private Text resourcesText;
        private Text killsText;
        private Text extractionText;
        private Text currencyText;
        private Text resourcesInventoryText;
        private RectTransform arrowLayer;
        private readonly List<Text> resourceArrows = new List<Text>();
        private PrototypePlayerController player;

        public PrototypeExpeditionRunController Source => source;

        public static PrototypeExpeditionRunHud CreateFor(PrototypeExpeditionRunController source)
        {
            if (source != null)
            {
                var existing = source.GetComponentInChildren<PrototypeExpeditionRunHud>(true);
                if (existing != null)
                {
                    existing.Configure(source);
                    return existing;
                }
            }

            var hudObject = new GameObject("Prototype Expedition HUD");
            if (source != null)
            {
                hudObject.transform.SetParent(source.transform, false);
            }

            var hud = hudObject.AddComponent<PrototypeExpeditionRunHud>();
            hud.Configure(source);
            return hud;
        }

        public void Configure(PrototypeExpeditionRunController newSource)
        {
            source = newSource;
            EnsureHud();
            Refresh();
        }

        private void Awake()
        {
            EnsureHud();
        }

        private void LateUpdate()
        {
            if (source == null)
            {
                source = FindFirstObjectByType<PrototypeExpeditionRunController>();
            }

            Refresh();
            UpdateResourceArrows();
        }

        private void OnEnable()
        {
            PrototypePersistentInventory.Changed += Refresh;
        }

        private void OnDisable()
        {
            PrototypePersistentInventory.Changed -= Refresh;
        }

        private void OnValidate()
        {
            panelSize.x = Mathf.Max(160f, panelSize.x);
            panelSize.y = Mathf.Max(118f, panelSize.y);
            resourceArrowDistanceFromPlayer = Mathf.Max(0f, resourceArrowDistanceFromPlayer);
            resourceArrowScreenPadding = Mathf.Max(0f, resourceArrowScreenPadding);
            resourceArrowWorldHeight = Mathf.Max(0f, resourceArrowWorldHeight);
        }

        private void EnsureHud()
        {
            if (resourcesText != null && killsText != null && extractionText != null && currencyText != null && resourcesInventoryText != null)
            {
                EnsureArrowLayer();
                return;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            var panel = new GameObject("Counters");
            panel.transform.SetParent(transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = anchorOffset;
            panelRect.sizeDelta = panelSize;

            var background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.45f);
            background.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var font = ResolveFont();
            resourcesText = CreateTextRow(panel.transform, "Objective", font);
            killsText = CreateTextRow(panel.transform, "Kills", font);
            extractionText = CreateTextRow(panel.transform, "Extraction", font);
            currencyText = CreateTextRow(panel.transform, "Currency", font);
            resourcesInventoryText = CreateTextRow(panel.transform, "Resources", font);
            EnsureArrowLayer();
        }

        private Text CreateTextRow(Transform parent, string name, Font font)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);

            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 22f);

            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 22f;
            layoutElement.minHeight = 20f;

            var text = row.AddComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void Refresh()
        {
            if (resourcesText == null || killsText == null || extractionText == null || currencyText == null || resourcesInventoryText == null || source == null)
            {
                return;
            }

            resourcesText.text = $"Objective: {source.CollectedResourceCount}/{source.RequiredResourceCount}";
            killsText.text = $"Kills: {source.KillCount}/{source.RequiredKillCount}";
            extractionText.text = source.IsComplete ? "Extraction: Active" : "Extraction: Locked";
            currencyText.text = $"Currency: {PrototypePersistentInventory.Currency}";
            resourcesInventoryText.text = $"Resources: {PrototypePersistentInventory.Resources}";
        }

        private void EnsureArrowLayer()
        {
            if (arrowLayer != null)
            {
                return;
            }

            var layer = new GameObject("Resource Arrows");
            layer.transform.SetParent(transform, false);
            arrowLayer = layer.AddComponent<RectTransform>();
            arrowLayer.anchorMin = Vector2.zero;
            arrowLayer.anchorMax = Vector2.one;
            arrowLayer.pivot = new Vector2(0.5f, 0.5f);
            arrowLayer.offsetMin = Vector2.zero;
            arrowLayer.offsetMax = Vector2.zero;
        }

        private void UpdateResourceArrows()
        {
            EnsureArrowLayer();

            var nodes = source != null ? source.ResourceNodes : null;
            var cameraToUse = Camera.main;
            player = player != null ? player : FindFirstObjectByType<PrototypePlayerController>();
            if (nodes == null || nodes.Length == 0 || cameraToUse == null || player == null)
            {
                HideResourceArrows(0);
                return;
            }

            var playerScreenPosition = cameraToUse.WorldToScreenPoint(player.transform.position + Vector3.up * resourceArrowWorldHeight);
            if (playerScreenPosition.z <= 0f)
            {
                HideResourceArrows(0);
                return;
            }

            EnsureResourceArrowCount(nodes.Length);
            var shownCount = 0;
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null || node.IsCollected)
                {
                    continue;
                }

                var nodeWorldPosition = node.transform.position + Vector3.up * resourceArrowWorldHeight;
                var viewportPosition = cameraToUse.WorldToViewportPoint(nodeWorldPosition);
                if (viewportPosition.z > 0f
                    && viewportPosition.x >= 0f
                    && viewportPosition.x <= 1f
                    && viewportPosition.y >= 0f
                    && viewportPosition.y <= 1f)
                {
                    continue;
                }

                var nodeScreenPosition = cameraToUse.WorldToScreenPoint(nodeWorldPosition);
                var direction = new Vector2(
                    nodeScreenPosition.x - playerScreenPosition.x,
                    nodeScreenPosition.y - playerScreenPosition.y);
                if (nodeScreenPosition.z < 0f)
                {
                    direction = -direction;
                }

                if (direction.sqrMagnitude <= 0.01f)
                {
                    direction = Vector2.up;
                }

                var arrowScreenPosition = new Vector2(playerScreenPosition.x, playerScreenPosition.y)
                    + direction.normalized * resourceArrowDistanceFromPlayer;
                var maxX = Mathf.Max(resourceArrowScreenPadding, Screen.width - resourceArrowScreenPadding);
                var maxY = Mathf.Max(resourceArrowScreenPadding, Screen.height - resourceArrowScreenPadding);
                arrowScreenPosition.x = Mathf.Clamp(arrowScreenPosition.x, resourceArrowScreenPadding, maxX);
                arrowScreenPosition.y = Mathf.Clamp(arrowScreenPosition.y, resourceArrowScreenPadding, maxY);

                PositionResourceArrow(resourceArrows[shownCount], arrowScreenPosition, direction);
                shownCount++;
            }

            HideResourceArrows(shownCount);
        }

        private void EnsureResourceArrowCount(int count)
        {
            var font = ResolveFont();
            while (resourceArrows.Count < count)
            {
                var arrow = new GameObject("Resource Arrow");
                arrow.transform.SetParent(arrowLayer, false);

                var rect = arrow.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(28f, 28f);

                var text = arrow.AddComponent<Text>();
                text.font = font;
                text.text = ">";
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = resourceArrowColor;
                text.raycastTarget = false;
                text.supportRichText = false;
                arrow.SetActive(false);
                resourceArrows.Add(text);
            }
        }

        private void PositionResourceArrow(Text arrow, Vector2 screenPosition, Vector2 direction)
        {
            if (arrow == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(arrowLayer, screenPosition, null, out var localPosition)
                && arrow.transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = localPosition;
            }

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            arrow.color = resourceArrowColor;
            arrow.gameObject.SetActive(true);
        }

        private void HideResourceArrows(int firstIndex)
        {
            for (var i = firstIndex; i < resourceArrows.Count; i++)
            {
                if (resourceArrows[i] != null)
                {
                    resourceArrows[i].gameObject.SetActive(false);
                }
            }
        }

        private Font ResolveFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                return font;
            }

            return Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI" }, 18);
        }
    }
}
