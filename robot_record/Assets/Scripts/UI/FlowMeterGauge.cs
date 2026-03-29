using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.UI
{
    public class FlowMeterGauge : MonoBehaviour
    {
        private const float MaxFlow = 100f;

        private FlowMeterManager _flowMeter;
        private Text _valueText;
        private Image _fillBar;

        public void Initialize(FlowMeterManager flowMeter, Transform parent)
        {
            _flowMeter = flowMeter;

            // Root panel
            var panel = HUDTheme.CreateBorderPanel("FlowPanel", parent, HUDTheme.BgSurface, HUDTheme.BorderDefault);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.offsetMin = new Vector2(4f, 0f);
            panelRect.offsetMax = new Vector2(0f, 0f);

            var inner = panel.transform.GetChild(0);

            // Vertical layout
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Label
            var label = HUDTheme.CreateLabel("FlowLabel", inner, "FLOW", 9);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // Value text
            _valueText = HUDTheme.CreateValueText("FlowValue", inner, "0.0 ml/min", 16);
            _valueText.fontStyle = FontStyle.Bold;
            _valueText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            // Progress bar background
            var barBg = HUDTheme.CreatePanel("BarBg", inner, HUDTheme.BgRoot);
            barBg.AddComponent<LayoutElement>().preferredHeight = 12f;

            // Fill bar (child of background, anchored left)
            var fillGO = HUDTheme.CreatePanel("BarFill", barBg.transform, HUDTheme.SuccessGreen);
            _fillBar = fillGO.GetComponent<Image>();
            _fillBar.type = Image.Type.Filled;
            _fillBar.fillMethod = Image.FillMethod.Horizontal;
            _fillBar.fillAmount = 0f;
            var fillRect = fillGO.GetComponent<RectTransform>();
            HUDTheme.StretchFill(fillRect);

            Refresh(0f);
        }

        private void Update()
        {
            if (_flowMeter == null) return;
            Refresh(_flowMeter.FlowRate);
        }

        private void Refresh(float rate)
        {
            float normalized = Mathf.Clamp01(rate / MaxFlow);

            if (_valueText != null)
                _valueText.text = $"{rate:F1} ml/min";

            if (_fillBar != null)
            {
                _fillBar.fillAmount = normalized;
                _fillBar.color = GetFlowColor(normalized);
            }
        }

        private static Color GetFlowColor(float t)
        {
            if (t < 0.33f)
                return HUDTheme.SuccessGreen;
            if (t < 0.66f)
                return HUDTheme.WarningOrange;
            return HUDTheme.DangerRed;
        }
    }
}
