using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.Controller;
using RobotMiddleware.Recording;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.UI
{
    /// <summary>
    /// Builds and manages the full-screen mission control HUD overlay.
    /// All UI is created programmatically in Start().
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // Manager references (resolved at runtime via FindObjectOfType)
        private RecordingManager _recordingManager;
        private MiddlewareController _middlewareController;
        private RealSenseManager _realSenseManager;
        private ViveTrackerManager _viveTrackerManager;
        private FlowMeterManager _flowMeterManager;

        // Header status
        private Image _relayDot;
        private Text _relayLabel;

        // 3D viewport
        private RawImage _viewportImage;
        private Camera _secondaryCamera;
        private RenderTexture _viewportRT;

        // Sub-panels (components on this same GO)
        private StatePanel _statePanel;
        private SensorPanel _sensorPanel;
        private CommandLogPanel _commandLogPanel;
        private FlowMeterGauge _flowMeterGauge;

        private void Start()
        {
            // Find all manager references at runtime
            _recordingManager = FindAnyObjectByType<RecordingManager>();
            _middlewareController = FindAnyObjectByType<MiddlewareController>();
            _realSenseManager = FindAnyObjectByType<RealSenseManager>();
            _viveTrackerManager = FindAnyObjectByType<ViveTrackerManager>();
            _flowMeterManager = FindAnyObjectByType<FlowMeterManager>();

            Debug.Log($"[HUDController] Found managers - Recording:{_recordingManager != null} MW:{_middlewareController != null} RS:{_realSenseManager != null} VT:{_viveTrackerManager != null} FM:{_flowMeterManager != null}");

            // Auto-start sensors if not already running
            if (_realSenseManager != null && !_realSenseManager.IsStreaming)
                _realSenseManager.StartStreaming();
            if (_flowMeterManager != null && !_flowMeterManager.IsConnected)
                _flowMeterManager.Connect(_flowMeterManager.PortName);

            BuildHUD();
        }

        private void BuildHUD()
        {
            // ═══════════════════════════════════════
            //  CANVAS
            // ═══════════════════════════════════════
            var canvasGO = new GameObject("HUDCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Full-screen background
            var bg = HUDTheme.CreatePanel("Background", canvasGO.transform, HUDTheme.BgRoot);
            HUDTheme.StretchFill(bg.GetComponent<RectTransform>());

            // Main vertical layout
            var mainLayout = bg.AddComponent<VerticalLayoutGroup>();
            mainLayout.padding = new RectOffset(12, 12, 8, 8);
            mainLayout.spacing = 8f;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = true;

            // ═══════════════════════════════════════
            //  HEADER BAR
            // ═══════════════════════════════════════
            BuildHeader(bg.transform);

            // ═══════════════════════════════════════
            //  MIDDLE SECTION (viewport + sensors)
            // ═══════════════════════════════════════
            var middleRow = new GameObject("MiddleRow", typeof(RectTransform));
            middleRow.transform.SetParent(bg.transform, false);
            var middleLayoutEl = middleRow.AddComponent<LayoutElement>();
            middleLayoutEl.flexibleHeight = 1f;

            var middleHL = middleRow.AddComponent<HorizontalLayoutGroup>();
            middleHL.spacing = 8f;
            middleHL.childForceExpandWidth = false;
            middleHL.childForceExpandHeight = true;
            middleHL.childControlWidth = true;
            middleHL.childControlHeight = true;

            // 3D Viewport (left, ~65% width)
            BuildViewport(middleRow.transform);

            // Sensor panel (right, ~35% width)
            var sensorContainer = new GameObject("SensorContainer", typeof(RectTransform));
            sensorContainer.transform.SetParent(middleRow.transform, false);
            sensorContainer.AddComponent<LayoutElement>().flexibleWidth = 0.35f;

            _sensorPanel = gameObject.AddComponent<SensorPanel>();
            _sensorPanel.Initialize(_realSenseManager, _viveTrackerManager, sensorContainer.transform);

            // ═══════════════════════════════════════
            //  BOTTOM SECTION (state + flow + log)
            // ═══════════════════════════════════════
            var bottomRow = new GameObject("BottomRow", typeof(RectTransform));
            bottomRow.transform.SetParent(bg.transform, false);
            bottomRow.AddComponent<LayoutElement>().preferredHeight = 120f;

            var bottomHL = bottomRow.AddComponent<HorizontalLayoutGroup>();
            bottomHL.spacing = 8f;
            bottomHL.childForceExpandWidth = false;
            bottomHL.childForceExpandHeight = true;
            bottomHL.childControlWidth = true;
            bottomHL.childControlHeight = true;

            // State + Flow combined container (left ~35%)
            var stateFlowContainer = new GameObject("StateFlowContainer", typeof(RectTransform));
            stateFlowContainer.transform.SetParent(bottomRow.transform, false);
            stateFlowContainer.AddComponent<LayoutElement>().flexibleWidth = 0.35f;

            _statePanel = gameObject.AddComponent<StatePanel>();
            _statePanel.Initialize(_recordingManager, stateFlowContainer.transform);

            _flowMeterGauge = gameObject.AddComponent<FlowMeterGauge>();
            _flowMeterGauge.Initialize(_flowMeterManager, stateFlowContainer.transform);

            // Command log (right ~65%)
            var logContainer = new GameObject("LogContainer", typeof(RectTransform));
            logContainer.transform.SetParent(bottomRow.transform, false);
            logContainer.AddComponent<LayoutElement>().flexibleWidth = 0.65f;

            _commandLogPanel = gameObject.AddComponent<CommandLogPanel>();
            _commandLogPanel.Initialize(_middlewareController, logContainer.transform);
        }

        // ─── Header ───
        private void BuildHeader(Transform parent)
        {
            var header = HUDTheme.CreateBorderPanel("Header", parent, HUDTheme.BgSurface, HUDTheme.BorderDefault);
            header.AddComponent<LayoutElement>().preferredHeight = 40f;

            var inner = header.transform.GetChild(0);
            var hl = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(14, 14, 0, 0);
            hl.spacing = 0f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;

            // Title
            var title = HUDTheme.CreateValueText("Title", inner, "ROBOT MIDDLEWARE", 14);
            title.color = HUDTheme.AccentCyan;
            title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;

            // Separator dash
            var dash = HUDTheme.CreateValueText("Dash", inner, " \u2014 ", 12);
            dash.color = HUDTheme.TextSecondary;
            dash.gameObject.AddComponent<LayoutElement>().preferredWidth = 30f;

            // Subtitle
            var sub = HUDTheme.CreateValueText("Subtitle", inner, "Action Node v1.0", 11);
            sub.color = HUDTheme.TextSecondary;
            var subLayout = sub.gameObject.AddComponent<LayoutElement>();
            subLayout.flexibleWidth = 1f;

            // Spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(inner, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 10f;

            // Relay status
            _relayDot = HUDTheme.CreateDot("RelayDot", inner, HUDTheme.TextSecondary, 8f);
            _relayDot.gameObject.AddComponent<LayoutElement>().preferredWidth = 8f;

            // Small gap
            var gap = new GameObject("Gap", typeof(RectTransform));
            gap.transform.SetParent(inner, false);
            gap.AddComponent<LayoutElement>().preferredWidth = 8f;

            _relayLabel = HUDTheme.CreateValueText("RelayLabel", inner, "RELAY: OFFLINE", 10);
            _relayLabel.color = HUDTheme.TextSecondary;
            _relayLabel.alignment = TextAnchor.MiddleRight;
            _relayLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;
        }

        // ─── 3D Viewport ───
        private void BuildViewport(Transform parent)
        {
            var vpContainer = HUDTheme.CreateBorderPanel("ViewportPanel", parent, HUDTheme.BgRoot, HUDTheme.BorderDefault);
            vpContainer.AddComponent<LayoutElement>().flexibleWidth = 0.65f;

            var inner = vpContainer.transform.GetChild(0);

            // Add label at top
            var vlayout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vlayout.padding = new RectOffset(10, 10, 6, 6);
            vlayout.spacing = 4f;
            vlayout.childForceExpandWidth = true;
            vlayout.childForceExpandHeight = false;
            vlayout.childControlWidth = true;
            vlayout.childControlHeight = true;

            var vpLabel = HUDTheme.CreateLabel("VPLabel", inner, "3D VIEWPORT", 9);
            vpLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // RawImage for render texture
            var imgGO = new GameObject("ViewportImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imgGO.transform.SetParent(inner, false);
            _viewportImage = imgGO.GetComponent<RawImage>();
            _viewportImage.color = Color.white;
            imgGO.AddComponent<LayoutElement>().flexibleHeight = 1f;

            SetupSecondaryCamera();
        }

        private void SetupSecondaryCamera()
        {
            // Create a RenderTexture
            _viewportRT = new RenderTexture(960, 540, 16);
            _viewportRT.name = "HUD_ViewportRT";

            // Try to find a secondary camera, or clone the main camera
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[HUDController] No main camera found for viewport");
                return;
            }

            // Create a secondary camera
            var camGO = new GameObject("HUD_SecondaryCamera");
            camGO.transform.SetParent(transform, false);
            _secondaryCamera = camGO.AddComponent<Camera>();

            // Copy main camera settings
            _secondaryCamera.CopyFrom(mainCam);
            _secondaryCamera.targetTexture = _viewportRT;
            _secondaryCamera.depth = mainCam.depth - 1;

            // Position it at a different angle
            camGO.transform.position = mainCam.transform.position + new Vector3(2f, 1f, -2f);
            camGO.transform.LookAt(Vector3.zero);

            // Don't render UI layer
            _secondaryCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));

            _viewportImage.texture = _viewportRT;
        }

        private void Update()
        {
            UpdateRelayStatus();
        }

        private void UpdateRelayStatus()
        {
            bool connected = _middlewareController != null && _middlewareController.IsConnected;

            if (_relayDot != null)
                _relayDot.color = connected ? HUDTheme.SuccessGreen : HUDTheme.TextSecondary;

            if (_relayLabel != null)
                _relayLabel.text = connected ? "RELAY: ONLINE" : "RELAY: OFFLINE";
        }

        private void OnDestroy()
        {
            if (_viewportRT != null)
            {
                _viewportRT.Release();
                Destroy(_viewportRT);
            }
            if (_secondaryCamera != null)
                Destroy(_secondaryCamera.gameObject);
        }
    }
}
