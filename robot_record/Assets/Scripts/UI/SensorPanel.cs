using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.UI
{
    public class SensorPanel : MonoBehaviour
    {
        private RealSenseManager _realSense;
        private ViveTrackerManager _viveTracker;

        private RawImage _rgbImage;
        private RawImage _depthImage;
        private Text _posText;
        private Text _rotText;
        private Image _trackingDot;
        private Text _trackingLabel;

        public void Initialize(RealSenseManager realSense, ViveTrackerManager viveTracker, Transform parent)
        {
            _realSense = realSense;
            _viveTracker = viveTracker;

            // Root panel
            var panel = HUDTheme.CreateBorderPanel("SensorPanel", parent, HUDTheme.BgSurface, HUDTheme.BorderDefault);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var inner = panel.transform.GetChild(0);

            // Vertical layout
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // ── REALSENSE D435i ──
            var rsLabel = HUDTheme.CreateLabel("RSLabel", inner, "SENSOR DATA", 9);
            rsLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            var rsTitle = HUDTheme.CreateValueText("RSTitle", inner, "REALSENSE D435i", 11);
            rsTitle.color = HUDTheme.AccentCyan;
            rsTitle.fontStyle = FontStyle.Bold;
            rsTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // RGB section
            var rgbLabel = HUDTheme.CreateLabel("RGBLabel", inner, "RGB", 8);
            rgbLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;

            var rgbHolder = HUDTheme.CreatePanel("RGBHolder", inner, HUDTheme.BgRoot);
            rgbHolder.AddComponent<LayoutElement>().preferredHeight = 90f;
            _rgbImage = CreateRawImage("RGBImage", rgbHolder.transform);

            // Depth section
            var depthLabel = HUDTheme.CreateLabel("DepthLabel", inner, "DEPTH", 8);
            depthLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;

            var depthHolder = HUDTheme.CreatePanel("DepthHolder", inner, HUDTheme.BgRoot);
            depthHolder.AddComponent<LayoutElement>().preferredHeight = 90f;
            _depthImage = CreateRawImage("DepthImage", depthHolder.transform);

            // ── VIVE TRACKER ──
            var vtTitle = HUDTheme.CreateValueText("VTTitle", inner, "VIVE TRACKER", 11);
            vtTitle.color = HUDTheme.AccentCyan;
            vtTitle.fontStyle = FontStyle.Bold;
            var vtTitleLayout = vtTitle.gameObject.AddComponent<LayoutElement>();
            vtTitleLayout.preferredHeight = 20f;

            // Position
            _posText = HUDTheme.CreateValueText("PosText", inner, "Pos: 0.00, 0.00, 0.00", 11);
            _posText.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // Rotation
            _rotText = HUDTheme.CreateValueText("RotText", inner, "Rot: 0.00, 0.00, 0.00", 11);
            _rotText.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // Tracking status row
            var trackRow = new GameObject("TrackRow", typeof(RectTransform));
            trackRow.transform.SetParent(inner, false);
            var trl = trackRow.AddComponent<HorizontalLayoutGroup>();
            trl.spacing = 6f;
            trl.childAlignment = TextAnchor.MiddleLeft;
            trl.childForceExpandWidth = false;
            trl.childForceExpandHeight = false;
            trl.childControlWidth = true;
            trl.childControlHeight = true;
            trackRow.AddComponent<LayoutElement>().preferredHeight = 18f;

            _trackingDot = HUDTheme.CreateDot("TrackDot", trackRow.transform, HUDTheme.TextSecondary, 8f);
            _trackingDot.gameObject.AddComponent<LayoutElement>().preferredWidth = 8f;

            _trackingLabel = HUDTheme.CreateValueText("TrackLabel", trackRow.transform, "No Tracker", 11);

            // Subscribe and set initial textures
            if (_realSense != null)
            {
                _realSense.OnFrameReady += OnFrameReady;
                // If already streaming, grab current textures
                if (_realSense.IsStreaming)
                {
                    if (_realSense.ColorTexture != null) _rgbImage.texture = _realSense.ColorTexture;
                    if (_realSense.DepthTexture != null) _depthImage.texture = _realSense.DepthTexture;
                }
            }
        }

        private RawImage CreateRawImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var ri = go.GetComponent<RawImage>();
            ri.color = Color.white;
            var rect = go.GetComponent<RectTransform>();
            HUDTheme.StretchFill(rect);
            return ri;
        }

        private void OnFrameReady(Texture2D color, Texture2D depth)
        {
            if (_rgbImage != null) _rgbImage.texture = color;
            if (_depthImage != null) _depthImage.texture = depth;
        }

        private void Update()
        {
            if (_viveTracker == null) return;

            var p = _viveTracker.Position;
            var e = _viveTracker.Rotation.eulerAngles;

            if (_posText != null)
                _posText.text = $"Pos: {p.x:F2}, {p.y:F2}, {p.z:F2}";
            if (_rotText != null)
                _rotText.text = $"Rot: {e.x:F2}, {e.y:F2}, {e.z:F2}";

            bool tracking = _viveTracker.IsTracking;
            if (_trackingDot != null)
                _trackingDot.color = tracking ? HUDTheme.SuccessGreen : HUDTheme.DangerRed;
            if (_trackingLabel != null)
                _trackingLabel.text = tracking ? "Tracking" : "Lost";
        }

        private void OnDestroy()
        {
            if (_realSense != null)
                _realSense.OnFrameReady -= OnFrameReady;
        }
    }
}
