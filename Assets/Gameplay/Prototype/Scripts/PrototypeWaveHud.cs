using UnityEngine;
using UnityEngine.UI;

namespace Islands.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeWaveHud : MonoBehaviour
    {
        [SerializeField]
        private PrototypeCombatSandbox source;

        [SerializeField]
        private Vector2 anchorOffset = new Vector2(18f, -18f);

        [SerializeField]
        private Vector2 panelSize = new Vector2(240f, 138f);

        private Text waveText;
        private Text timerText;
        private Text corruptionText;
        private Text aliveText;
        private Text killedText;
        private Image corruptionFill;

        public PrototypeCombatSandbox Source => source;

        public static PrototypeWaveHud CreateFor(PrototypeCombatSandbox source)
        {
            if (source != null)
            {
                var existing = source.GetComponentInChildren<PrototypeWaveHud>(true);
                if (existing != null)
                {
                    existing.Configure(source);
                    return existing;
                }
            }

            var hudObject = new GameObject("Prototype Wave HUD");
            if (source != null)
            {
                hudObject.transform.SetParent(source.transform, false);
            }

            var hud = hudObject.AddComponent<PrototypeWaveHud>();
            hud.Configure(source);
            return hud;
        }

        public void Configure(PrototypeCombatSandbox newSource)
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
                source = FindFirstObjectByType<PrototypeCombatSandbox>();
            }

            Refresh();
        }

        private void OnValidate()
        {
            panelSize.x = Mathf.Max(120f, panelSize.x);
            panelSize.y = Mathf.Max(108f, panelSize.y);
        }

        private void EnsureHud()
        {
            if (waveText != null
                && timerText != null
                && corruptionText != null
                && aliveText != null
                && killedText != null
                && corruptionFill != null)
            {
                return;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

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
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var font = ResolveFont();
            waveText = CreateTextRow(panel.transform, "Wave", font);
            timerText = CreateTextRow(panel.transform, "Spawn Timer", font);
            corruptionText = CreateTextRow(panel.transform, "Corruption", font);
            corruptionFill = CreateProgressBar(panel.transform, "Corruption Meter");
            aliveText = CreateTextRow(panel.transform, "Alive", font);
            killedText = CreateTextRow(panel.transform, "Killed", font);
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

        private Image CreateProgressBar(Transform parent, string name)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);

            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 14f);

            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 14f;
            layoutElement.minHeight = 12f;

            var background = row.AddComponent<Image>();
            background.color = new Color(0.16f, 0.04f, 0.04f, 0.9f);
            background.raycastTarget = false;

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(row.transform, false);
            var fillRect = fillObject.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            var fill = fillObject.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.color = new Color(0.92f, 0.22f, 0.12f, 0.95f);
            fill.raycastTarget = false;
            return fill;
        }

        private void Refresh()
        {
            if (waveText == null
                || timerText == null
                || corruptionText == null
                || aliveText == null
                || killedText == null
                || corruptionFill == null
                || source == null)
            {
                return;
            }

            waveText.text = $"Wave: {source.CurrentWave}";
            timerText.text = source.IsRunActive
                ? $"Spawn Timer: {source.CurrentWaveCountdown:0.0}s"
                : "Spawn Timer: --";
            corruptionText.text = $"Corruption Lv {source.CorruptionLevel}: {source.CorruptionMeterValue:0}/{source.CorruptionMeterMax:0}";
            corruptionFill.fillAmount = source.CorruptionMeterNormalized;
            aliveText.text = $"Alive: {source.AliveEnemyCount}";
            killedText.text = $"Killed: {source.TotalEnemiesKilled}";
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
