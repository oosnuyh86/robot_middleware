using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RobotMiddleware.Controller;
using RobotMiddleware.Recording;
using RobotMiddleware.Sensors;

namespace RobotMiddleware.UI
{
    /// <summary>
    /// Builds the full-screen mission control HUD Canvas hierarchy in Start().
    /// Creates all GameObjects/components, then attaches panel MonoBehaviours
    /// (StatePanel, SensorPanel, CommandLogPanel, FlowMeterGauge) and wires
    /// their public fields before calling Initialize().
    /// Layout matches the reference design at 1920x1080 pixel-perfect.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ── Manager references (resolved at runtime) ──
        private RecordingManager _recordingManager;
        private MiddlewareController _middlewareController;
        private RealSenseManager _realSenseManager;
        private ViveTrackerManager _viveTrackerManager;
        private FlowMeterManager _flowMeterManager;

        // ── Header ──
        private Image _relayDot;
        private TextMeshProUGUI _relayLabel;

        // ── 3D Viewport ──
        private RawImage _viewportImage;
        private Camera _secondaryCamera;
        private RenderTexture _viewportRT;

        // ── Inline UI references (TextMeshPro) ──
        private Image _stateDot;
        private TextMeshProUGUI _stateNameText;
        private TextMeshProUGUI _recordIdText;
        private TextMeshProUGUI _flowValueText;
        private Image _flowBarFill;
        private TextMeshProUGUI _flowStatusText;
        private TextMeshProUGUI _logText;
        private TextMeshProUGUI _posValueText;
        private TextMeshProUGUI _rotValueText;
        private Image _trackingDot;
        private TextMeshProUGUI _trackingStatusText;
        private RawImage _rgbImage;
        private RawImage _depthImage;
        private System.Collections.Generic.List<string> _logEntries = new System.Collections.Generic.List<string>();
        private const int MaxLogEntries = 20;

        private void Start()
        {
            _recordingManager = FindAnyObjectByType<RecordingManager>();
            _middlewareController = FindAnyObjectByType<MiddlewareController>();
            _realSenseManager = FindAnyObjectByType<RealSenseManager>();
            _viveTrackerManager = FindAnyObjectByType<ViveTrackerManager>();
            _flowMeterManager = FindAnyObjectByType<FlowMeterManager>();

            Debug.Log($"[HUDController] Found managers - Recording:{_recordingManager != null} MW:{_middlewareController != null} RS:{_realSenseManager != null} VT:{_viveTrackerManager != null} FM:{_flowMeterManager != null}");

            if (_realSenseManager != null && !_realSenseManager.IsStreaming)
                _realSenseManager.StartStreaming();
            if (_flowMeterManager != null && !_flowMeterManager.IsConnected)
                _flowMeterManager.Connect(_flowMeterManager.PortName);

            BuildHUD();
        }

        // ═══════════════════════════════════════════════════════════
        //  BUILD THE ENTIRE UI HIERARCHY
        // ═══════════════════════════════════════════════════════════

        private void BuildHUD()
        {
            // ── Canvas ──
            var canvasGO = new GameObject("HUDCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Root panel (full screen, bg-root, padding 8px) ──
            var root = HUDTheme.CreatePanel("Root", canvasGO.transform, HUDTheme.BgRoot);
            HUDTheme.StretchFill(root.GetComponent<RectTransform>());

            var rootLayout = root.AddComponent<VerticalLayoutGroup>();
            int pad = (int)HUDTheme.RootPadding;
            rootLayout.padding = new RectOffset(pad, pad, pad, pad);
            rootLayout.spacing = HUDTheme.PanelGap;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;

            // ── Header (48px) ──
            BuildHeader(root.transform);

            // ── Main area (flex fill, horizontal: left 65% + right 35%) ──
            var main = new GameObject("Main", typeof(RectTransform));
            main.transform.SetParent(root.transform, false);
            main.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var mainHL = main.AddComponent<HorizontalLayoutGroup>();
            mainHL.spacing = HUDTheme.PanelGap;
            mainHL.childForceExpandWidth = false;
            mainHL.childForceExpandHeight = true;
            mainHL.childControlWidth = true;
            mainHL.childControlHeight = true;

            // ── Left column (65%): viewport + bottom row ──
            var leftCol = new GameObject("LeftCol", typeof(RectTransform));
            leftCol.transform.SetParent(main.transform, false);
            leftCol.AddComponent<LayoutElement>().flexibleWidth = HUDTheme.LeftColFlex;

            var leftVL = leftCol.AddComponent<VerticalLayoutGroup>();
            leftVL.spacing = HUDTheme.PanelGap;
            leftVL.childForceExpandWidth = true;
            leftVL.childForceExpandHeight = false;
            leftVL.childControlWidth = true;
            leftVL.childControlHeight = true;

            BuildViewport(leftCol.transform);
            BuildBottomRow(leftCol.transform);

            // ── Right column (35%): sensor panel + tracker panel ──
            var rightCol = new GameObject("RightCol", typeof(RectTransform));
            rightCol.transform.SetParent(main.transform, false);
            rightCol.AddComponent<LayoutElement>().flexibleWidth = HUDTheme.RightColFlex;

            var rightVL = rightCol.AddComponent<VerticalLayoutGroup>();
            rightVL.spacing = HUDTheme.PanelGap;
            rightVL.childForceExpandWidth = true;
            rightVL.childForceExpandHeight = false;
            rightVL.childControlWidth = true;
            rightVL.childControlHeight = true;

            BuildRightColumn(rightCol.transform);
        }

        // ───────────────────────────────────────────────────────────
        //  HEADER BAR (48px)
        // ───────────────────────────────────────────────────────────

        private void BuildHeader(Transform parent)
        {
            var header = HUDTheme.CreateBorderPanel("Header", parent, HUDTheme.BgSurface, HUDTheme.Border);
            header.AddComponent<LayoutElement>().preferredHeight = HUDTheme.HeaderHeight;

            var inner = header.transform.GetChild(0);
            var hl = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(20, 20, 0, 0);
            hl.spacing = 0f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;

            // Title: 15px, Bold, cyan
            var title = HUDTheme.CreateText("Title", inner, "ROBOT MIDDLEWARE",
                HUDTheme.FontHeaderTitle, HUDTheme.Cyan, TextAlignmentOptions.MidlineLeft, true);
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;

            AddSpacer(inner, 16f);

            // Subtitle: 13px, Regular, text-3
            var sub = HUDTheme.CreateText("Subtitle", inner,
                "\u2014 Action Node v1.0", HUDTheme.FontHeaderSubtitle, HUDTheme.Text3);
            sub.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            AddFlexSpacer(inner);

            // Relay status indicator badge
            var badge = HUDTheme.CreatePanel("RelayBadge", inner, HUDTheme.RedGlow);
            var badgeLE = badge.AddComponent<LayoutElement>();
            badgeLE.preferredHeight = 30f;
            badgeLE.preferredWidth = 160f;

            var badgeHL = badge.AddComponent<HorizontalLayoutGroup>();
            badgeHL.padding = new RectOffset(14, 14, 6, 6);
            badgeHL.spacing = 8f;
            badgeHL.childAlignment = TextAnchor.MiddleLeft;
            badgeHL.childForceExpandWidth = false;
            badgeHL.childForceExpandHeight = false;
            badgeHL.childControlWidth = true;
            badgeHL.childControlHeight = true;

            _relayDot = HUDTheme.CreateDot("RelayDot", badge.transform, HUDTheme.Text2, 8f);
            _relayDot.gameObject.AddComponent<LayoutElement>().preferredWidth = 8f;

            _relayLabel = HUDTheme.CreateText("RelayLabel", badge.transform, "RELAY: OFFLINE",
                HUDTheme.FontStatusIndicator, HUDTheme.Text2, TextAlignmentOptions.MidlineLeft, true);
            _relayLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        // ───────────────────────────────────────────────────────────
        //  3D VIEWPORT (flex fill in left column)
        // ───────────────────────────────────────────────────────────

        private void BuildViewport(Transform parent)
        {
            var vp = HUDTheme.CreateBorderPanel("ViewportPanel", parent, HUDTheme.BgSurface, HUDTheme.Border);
            vp.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var inner = vp.transform.GetChild(0);

            // Overlay label at top-left
            var label = HUDTheme.CreateText("VPLabel", inner,
                HUDTheme.SpaceOut("3D VIEWPORT"), HUDTheme.FontXS, HUDTheme.Text3,
                TextAlignmentOptions.TopLeft, true);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 1);
            labelRect.anchoredPosition = new Vector2(16, -12);
            labelRect.sizeDelta = new Vector2(300, 20);

            // RawImage fills the panel
            _viewportImage = HUDTheme.CreateRawImage("ViewportImage", inner);
            HUDTheme.StretchFill(_viewportImage.GetComponent<RectTransform>());

            SetupSecondaryCamera();
        }

        private void SetupSecondaryCamera()
        {
            _viewportRT = new RenderTexture(960, 540, 16);
            _viewportRT.name = "HUD_ViewportRT";

            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[HUDController] No main camera found for viewport");
                return;
            }

            var camGO = new GameObject("HUD_SecondaryCamera");
            camGO.transform.SetParent(transform, false);
            _secondaryCamera = camGO.AddComponent<Camera>();
            _secondaryCamera.CopyFrom(mainCam);
            _secondaryCamera.targetTexture = _viewportRT;
            _secondaryCamera.depth = mainCam.depth - 1;
            _secondaryCamera.clearFlags = CameraClearFlags.SolidColor;
            _secondaryCamera.backgroundColor = HUDTheme.BgRoot;

            camGO.transform.position = mainCam.transform.position + new Vector3(2f, 1f, -2f);
            camGO.transform.LookAt(Vector3.zero);

            _secondaryCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));

            _viewportImage.texture = _viewportRT;
        }

        // ───────────────────────────────────────────────────────────
        //  BOTTOM ROW (140px: state 320px + flow 280px + log flex)
        // ───────────────────────────────────────────────────────────

        private void BuildBottomRow(Transform parent)
        {
            var row = new GameObject("BottomRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = HUDTheme.BottomRowHeight;

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = HUDTheme.PanelGap;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;
            hl.childControlWidth = true;
            hl.childControlHeight = true;

            BuildStatePanel(row.transform);
            BuildFlowPanel(row.transform);
            BuildLogPanel(row.transform);
        }

        // ── STATE PANEL (320px wide) ──

        private void BuildStatePanel(Transform parent)
        {
            var panel = HUDTheme.CreateBorderPanel("StatePanel", parent, HUDTheme.BgSurface, HUDTheme.Border);
            panel.AddComponent<LayoutElement>().preferredWidth = HUDTheme.StatePanelWidth;

            var inner = panel.transform.GetChild(0);
            var vl = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(20, 20, 16, 16);
            vl.spacing = 8f;
            vl.childAlignment = TextAnchor.MiddleLeft;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;

            // Panel label: 11px, SemiBold, text-3
            var label = HUDTheme.CreateText("StateLabel", inner,
                HUDTheme.SpaceOut("STATE"), HUDTheme.FontXS, HUDTheme.Text3,
                TextAlignmentOptions.MidlineLeft, true);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // State badge row: dot + name
            var badgeRow = new GameObject("StateBadge", typeof(RectTransform));
            badgeRow.transform.SetParent(inner, false);
            var badgeHL = badgeRow.AddComponent<HorizontalLayoutGroup>();
            badgeHL.spacing = 12f;
            badgeHL.childAlignment = TextAnchor.MiddleLeft;
            badgeHL.childForceExpandWidth = false;
            badgeHL.childForceExpandHeight = false;
            badgeHL.childControlWidth = true;
            badgeHL.childControlHeight = true;
            badgeRow.AddComponent<LayoutElement>().preferredHeight = 36f;

            // State dot (14px)
            var stateDot = HUDTheme.CreateDot("StateDot", badgeRow.transform, HUDTheme.Text2, 14f);
            var dotLE = stateDot.gameObject.AddComponent<LayoutElement>();
            dotLE.preferredWidth = 14f;
            dotLE.preferredHeight = 14f;

            // State name: 28px, Bold, text-1
            var stateNameText = HUDTheme.CreateText("StateName", badgeRow.transform, "PENDING",
                HUDTheme.FontXL, HUDTheme.Text1, TextAlignmentOptions.MidlineLeft, true);
            stateNameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Record ID: 13px, text-2
            var recordIdText = HUDTheme.CreateText("RecordId", inner, "Record: \u2014",
                HUDTheme.FontSM, HUDTheme.Text2);
            recordIdText.richText = true;
            recordIdText.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

            // Store references for inline updates
            _stateDot = stateDot;
            _stateNameText = stateNameText;
            _recordIdText = recordIdText;
            if (_recordingManager != null)
                _recordingManager.OnStateChanged += OnStateChanged;
        }

        // ── FLOW METER PANEL (280px wide) ──

        private void BuildFlowPanel(Transform parent)
        {
            var panel = HUDTheme.CreateBorderPanel("FlowPanel", parent, HUDTheme.BgSurface, HUDTheme.Border);
            panel.AddComponent<LayoutElement>().preferredWidth = HUDTheme.FlowPanelWidth;

            var inner = panel.transform.GetChild(0);
            var vl = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(20, 20, 16, 16);
            vl.spacing = 10f;
            vl.childAlignment = TextAnchor.MiddleLeft;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;

            // Panel label
            var label = HUDTheme.CreateText("FlowLabel", inner,
                HUDTheme.SpaceOut("FLOW"), HUDTheme.FontXS, HUDTheme.Text3,
                TextAlignmentOptions.MidlineLeft, true);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // Flow value row: value + unit
            var valueRow = new GameObject("FlowValueRow", typeof(RectTransform));
            valueRow.transform.SetParent(inner, false);
            var valueHL = valueRow.AddComponent<HorizontalLayoutGroup>();
            valueHL.spacing = 4f;
            valueHL.childAlignment = TextAnchor.LowerLeft;
            valueHL.childForceExpandWidth = false;
            valueHL.childForceExpandHeight = false;
            valueHL.childControlWidth = true;
            valueHL.childControlHeight = true;
            valueRow.AddComponent<LayoutElement>().preferredHeight = 38f;

            // Flow value: 32px, Bold, text-1
            var flowValueText = HUDTheme.CreateText("FlowValue", valueRow.transform, "0.0",
                HUDTheme.Font2XL, HUDTheme.Text1, TextAlignmentOptions.BottomLeft, true);
            flowValueText.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            // Flow unit: 16px, Regular, text-2
            var flowUnitText = HUDTheme.CreateText("FlowUnit", valueRow.transform, "ml/min",
                HUDTheme.FontMD, HUDTheme.Text2, TextAlignmentOptions.BottomLeft);
            flowUnitText.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            // Flow bar track
            var barTrack = HUDTheme.CreateBorderPanel("FlowBarTrack", inner, HUDTheme.BgPanel, HUDTheme.Border);
            barTrack.AddComponent<LayoutElement>().preferredHeight = 10f;
            var barTrackRect = barTrack.transform.GetChild(0).GetComponent<RectTransform>();

            var barInner = barTrack.transform.GetChild(0);

            // Flow bar fill
            var fillGO = HUDTheme.CreatePanel("FlowBarFill", barInner, HUDTheme.Yellow);
            var flowBarFill = fillGO.GetComponent<Image>();
            flowBarFill.type = Image.Type.Filled;
            flowBarFill.fillMethod = Image.FillMethod.Horizontal;
            flowBarFill.fillAmount = 0f;
            var fillRect = fillGO.GetComponent<RectTransform>();
            HUDTheme.StretchFill(fillRect);

            // Flow status line: 11px, text-3
            var flowStatusText = HUDTheme.CreateText("FlowStatus", inner, "Alicat MC-500 / COM3",
                HUDTheme.FontXS, HUDTheme.Text3);
            flowStatusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;

            // Store references for inline updates
            _flowValueText = flowValueText;
            _flowBarFill = flowBarFill;
            _flowStatusText = flowStatusText;
            if (_flowMeterManager != null)
                _flowMeterManager.OnFlowRateUpdated += OnFlowRateUpdated;
        }

        // ── COMMAND LOG PANEL (flex fill) ──

        private void BuildLogPanel(Transform parent)
        {
            var panel = HUDTheme.CreateBorderPanel("LogPanel", parent, HUDTheme.BgSurface, HUDTheme.Border);
            panel.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var inner = panel.transform.GetChild(0);
            var vl = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(16, 16, 12, 12);
            vl.spacing = 4f;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;

            // Panel label
            var label = HUDTheme.CreateText("LogLabel", inner,
                HUDTheme.SpaceOut("COMMAND LOG"), HUDTheme.FontXS, HUDTheme.Text3,
                TextAlignmentOptions.MidlineLeft, true);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // Log text fills remaining space
            var logText = HUDTheme.CreateText("LogEntries", inner, "",
                HUDTheme.FontLogEntry, HUDTheme.Text2, TextAlignmentOptions.BottomLeft);
            logText.richText = true;
            logText.overflowMode = TextOverflowModes.Overflow;
            logText.textWrappingMode = TextWrappingModes.Normal;
            logText.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // Store references for inline updates
            _logText = logText;
            if (_middlewareController != null)
            {
                _middlewareController.OnMessageReceived += msg => AddLogEntry(msg);
                _middlewareController.OnStatusChanged += status => AddLogEntry(status);
            }
        }

        // ───────────────────────────────────────────────────────────
        //  RIGHT COLUMN: RealSense (flex) + Tracker (220px)
        //  Both wired into a single SensorPanel MonoBehaviour
        // ───────────────────────────────────────────────────────────

        private void BuildRightColumn(Transform parent)
        {
            // We build two visual panels but wire them into one SensorPanel behaviour

            // ── RealSense sensor panel (flex fill) ──
            var rsPanel = HUDTheme.CreateBorderPanel("SensorPanel", parent, HUDTheme.BgSurface, HUDTheme.Border);
            rsPanel.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var rsInner = rsPanel.transform.GetChild(0);
            var rsVL = rsInner.gameObject.AddComponent<VerticalLayoutGroup>();
            rsVL.padding = new RectOffset(16, 16, 16, 16);
            rsVL.spacing = 12f;
            rsVL.childForceExpandWidth = true;
            rsVL.childForceExpandHeight = false;
            rsVL.childControlWidth = true;
            rsVL.childControlHeight = true;

            // RealSense header row
            var rsHeaderRow = new GameObject("SensorHeader", typeof(RectTransform));
            rsHeaderRow.transform.SetParent(rsInner, false);
            var rsHeaderHL = rsHeaderRow.AddComponent<HorizontalLayoutGroup>();
            rsHeaderHL.spacing = 8f;
            rsHeaderHL.childAlignment = TextAnchor.MiddleLeft;
            rsHeaderHL.childForceExpandWidth = false;
            rsHeaderHL.childForceExpandHeight = false;
            rsHeaderHL.childControlWidth = true;
            rsHeaderHL.childControlHeight = true;
            rsHeaderRow.AddComponent<LayoutElement>().preferredHeight = 20f;

            var rsTitle = HUDTheme.CreateText("SensorTitle", rsHeaderRow.transform,
                HUDTheme.SpaceOut("REALSENSE D435I"), HUDTheme.FontSM, HUDTheme.Cyan,
                TextAlignmentOptions.MidlineLeft, true);
            rsTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var rsDot = HUDTheme.CreateDot("SensorDot", rsHeaderRow.transform, HUDTheme.Green, 6f);
            rsDot.gameObject.AddComponent<LayoutElement>().preferredWidth = 6f;

            var rsStatusLabel = HUDTheme.CreateText("SensorStatus", rsHeaderRow.transform,
                "STREAMING", HUDTheme.FontXS, HUDTheme.Green, TextAlignmentOptions.MidlineRight);
            rsStatusLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 80f;

            // Feed grid: 2 columns (RGB + Depth)
            // Feed grid: 2 columns side by side, fixed height for 4:3 aspect
            var feedGrid = new GameObject("FeedGrid", typeof(RectTransform));
            feedGrid.transform.SetParent(rsInner, false);
            var feedGridLE = feedGrid.AddComponent<LayoutElement>();
            feedGridLE.preferredHeight = 160f; // Fixed height — not stretchy
            feedGridLE.flexibleHeight = 0f;

            var gridHL = feedGrid.AddComponent<HorizontalLayoutGroup>();
            gridHL.spacing = HUDTheme.PanelGap;
            gridHL.childForceExpandWidth = true;
            gridHL.childForceExpandHeight = true;
            gridHL.childControlWidth = true;
            gridHL.childControlHeight = true;

            var rgbFeedLabel = BuildFeedBox(feedGrid.transform, "RGB", out var rgbImage);
            var depthFeedLabel = BuildFeedBox(feedGrid.transform, "DEPTH", out var depthImage);

            // ── Tracker panel (220px) ──
            var vtPanel = HUDTheme.CreateBorderPanel("TrackerPanel", parent, HUDTheme.BgSurface, HUDTheme.Border);
            vtPanel.AddComponent<LayoutElement>().preferredHeight = HUDTheme.TrackerPanelHeight;

            var vtInner = vtPanel.transform.GetChild(0);
            var vtVL = vtInner.gameObject.AddComponent<VerticalLayoutGroup>();
            vtVL.padding = new RectOffset(20, 20, 16, 16);
            vtVL.spacing = 14f;
            vtVL.childForceExpandWidth = true;
            vtVL.childForceExpandHeight = false;
            vtVL.childControlWidth = true;
            vtVL.childControlHeight = true;

            // Tracker header row
            var vtHeaderRow = new GameObject("TrackerHeader", typeof(RectTransform));
            vtHeaderRow.transform.SetParent(vtInner, false);
            var vtHeaderHL = vtHeaderRow.AddComponent<HorizontalLayoutGroup>();
            vtHeaderHL.spacing = 8f;
            vtHeaderHL.childAlignment = TextAnchor.MiddleLeft;
            vtHeaderHL.childForceExpandWidth = false;
            vtHeaderHL.childForceExpandHeight = false;
            vtHeaderHL.childControlWidth = true;
            vtHeaderHL.childControlHeight = true;
            vtHeaderRow.AddComponent<LayoutElement>().preferredHeight = 20f;

            var vtTitle = HUDTheme.CreateText("TrackerTitle", vtHeaderRow.transform,
                HUDTheme.SpaceOut("VIVE TRACKER"), HUDTheme.FontSM, HUDTheme.Cyan,
                TextAlignmentOptions.MidlineLeft, true);
            vtTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var vtDot = HUDTheme.CreateDot("TrackerActiveDot", vtHeaderRow.transform, HUDTheme.Green, 6f);
            vtDot.gameObject.AddComponent<LayoutElement>().preferredWidth = 6f;

            var vtStatusLabel = HUDTheme.CreateText("TrackerActiveLabel", vtHeaderRow.transform,
                "ACTIVE", HUDTheme.FontXS, HUDTheme.Green, TextAlignmentOptions.MidlineRight);
            vtStatusLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 60f;

            // Tracker data rows
            var dataGroup = new GameObject("TrackerData", typeof(RectTransform));
            dataGroup.transform.SetParent(vtInner, false);
            var dataVL = dataGroup.AddComponent<VerticalLayoutGroup>();
            dataVL.spacing = 10f;
            dataVL.childForceExpandWidth = true;
            dataVL.childForceExpandHeight = false;
            dataVL.childControlWidth = true;
            dataVL.childControlHeight = true;
            dataGroup.AddComponent<LayoutElement>().flexibleHeight = 1f;

            BuildTrackerDataRow(dataGroup.transform, "POS", out var posLabel, out var posValue);
            BuildTrackerDataRow(dataGroup.transform, "ROT", out var rotLabel, out var rotValue);

            // Tracking status bar
            var statusBar = HUDTheme.CreatePanel("TrackerStatusBar", vtInner, new Color(0f, 0.902f, 0.463f, 0.06f));
            statusBar.AddComponent<LayoutElement>().preferredHeight = 36f;

            var statusHL = statusBar.AddComponent<HorizontalLayoutGroup>();
            statusHL.padding = new RectOffset(16, 16, 10, 10);
            statusHL.spacing = 10f;
            statusHL.childAlignment = TextAnchor.MiddleLeft;
            statusHL.childForceExpandWidth = false;
            statusHL.childForceExpandHeight = false;
            statusHL.childControlWidth = true;
            statusHL.childControlHeight = true;

            var trackingDot = HUDTheme.CreateDot("TrackingDot", statusBar.transform, HUDTheme.Green, 10f);
            trackingDot.gameObject.AddComponent<LayoutElement>().preferredWidth = 10f;

            var trackingStatusText = HUDTheme.CreateText("TrackingStatus", statusBar.transform,
                "TRACKING", HUDTheme.FontTrackerStatus, HUDTheme.Green,
                TextAlignmentOptions.MidlineLeft, true);
            trackingStatusText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Store references for inline updates
            _rgbImage = rgbImage;
            _depthImage = depthImage;
            _posValueText = posValue;
            _rotValueText = rotValue;
            _trackingDot = trackingDot;
            _trackingStatusText = trackingStatusText;
            if (_viveTrackerManager != null)
                _viveTrackerManager.OnPoseUpdated += OnPoseUpdated;
        }

        // ── Feed box builder (returns label Text, outputs RawImage) ──

        private TextMeshProUGUI BuildFeedBox(Transform parent, string labelText, out RawImage feedImage)
        {
            var box = HUDTheme.CreateBorderPanel("Feed_" + labelText, parent, HUDTheme.BgPanel, HUDTheme.Border);
            box.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var inner = box.transform.GetChild(0);
            var vl = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 0f;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;

            // Feed label: 10px, SemiBold, text-3, uppercase spaced
            var label = HUDTheme.CreateText("FeedLabel", inner,
                HUDTheme.SpaceOut(labelText), HUDTheme.FontFeedLabel, HUDTheme.Text3,
                TextAlignmentOptions.MidlineLeft, true);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 24f;

            // Separator
            var sep = HUDTheme.CreatePanel("FeedSep", inner, HUDTheme.Border);
            sep.AddComponent<LayoutElement>().preferredHeight = 1f;

            // Feed image area
            feedImage = HUDTheme.CreateRawImage("FeedImage", inner);
            feedImage.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            return label;
        }

        // ── Tracker data row builder ──

        private void BuildTrackerDataRow(Transform parent, string label,
            out TextMeshProUGUI labelText, out TextMeshProUGUI valueText)
        {
            var row = new GameObject("DataRow_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            row.AddComponent<LayoutElement>().preferredHeight = 24f;

            // Axis label: 12px, Bold, text-3, 40px wide
            labelText = HUDTheme.CreateText("Label_" + label, row.transform,
                label, HUDTheme.FontTrackerLabel, HUDTheme.Text3,
                TextAlignmentOptions.MidlineLeft, true);
            labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;

            // Value: 20px, Medium, text-1, rich text for colored axes
            valueText = HUDTheme.CreateText("Value_" + label, row.transform,
                "", HUDTheme.FontLG, HUDTheme.Text1);
            valueText.richText = true;
            valueText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        // ───────────────────────────────────────────────────────────
        //  UTILITY
        // ───────────────────────────────────────────────────────────

        private static void AddSpacer(Transform parent, float width)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            spacer.AddComponent<LayoutElement>().preferredWidth = width;
        }

        private static void AddFlexSpacer(Transform parent)
        {
            var spacer = new GameObject("FlexSpacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 10f;
        }

        // ───────────────────────────────────────────────────────────
        //  UPDATE (only relay status — panels handle their own)
        // ───────────────────────────────────────────────────────────

        private void Update()
        {
            UpdateRelayStatus();
            UpdateSensors();
        }

        private void UpdateRelayStatus()
        {
            bool connected = _middlewareController != null && _middlewareController.IsConnected;

            if (_relayDot != null)
                _relayDot.color = connected ? HUDTheme.Green : HUDTheme.Text2;

            if (_relayLabel != null)
            {
                _relayLabel.text = connected ? "RELAY: ONLINE" : "RELAY: OFFLINE";
                _relayLabel.color = connected ? HUDTheme.Green : HUDTheme.Red;
            }
        }

        private void OnStateChanged(Models.RecordingState state)
        {
            if (_stateDot != null)
                _stateDot.color = HUDTheme.GetStateColor(state);
            if (_stateNameText != null)
                _stateNameText.text = HUDTheme.GetStateDisplayName(state);
            if (_recordIdText != null && _recordingManager != null)
                _recordIdText.text = string.IsNullOrEmpty(_recordingManager.RecordId)
                    ? "Record: \u2014"
                    : $"Record: <color=#e8eaf0>{_recordingManager.RecordId}</color>";
        }

        private void OnFlowRateUpdated(float rate)
        {
            if (_flowValueText != null)
                _flowValueText.text = rate.ToString("F1");
            if (_flowBarFill != null)
            {
                _flowBarFill.fillAmount = Mathf.Clamp01(rate / 100f);
                float pct = rate / 100f;
                _flowBarFill.color = pct < 0.33f ? HUDTheme.Green : pct < 0.66f ? HUDTheme.Yellow : HUDTheme.Red;
            }
        }

        private void OnPoseUpdated(Vector3 pos, Quaternion rot)
        {
            if (_posValueText != null)
            {
                var euler = rot.eulerAngles;
                _posValueText.text = $"<color=#ff3d57><size=13>x</size></color>{pos.x:F3}  <color=#00e676><size=13>y</size></color>{pos.y:F3}  <color=#448aff><size=13>z</size></color>{pos.z:F3}";
            }
            if (_rotValueText != null)
            {
                var euler = rot.eulerAngles;
                _rotValueText.text = $"<color=#ff3d57><size=13>x</size></color>{euler.x:F1}\u00b0  <color=#00e676><size=13>y</size></color>{euler.y:F1}\u00b0  <color=#448aff><size=13>z</size></color>{euler.z:F1}\u00b0";
            }
        }

        private void UpdateSensors()
        {
            if (_realSenseManager != null)
            {
                if (_rgbImage != null && _realSenseManager.ColorTexture != null)
                    _rgbImage.texture = _realSenseManager.ColorTexture;
                if (_depthImage != null && _realSenseManager.DepthTexture != null)
                    _depthImage.texture = _realSenseManager.DepthTexture;
            }
        }

        private void AddLogEntry(string message)
        {
            string time = System.DateTime.Now.ToString("HH:mm:ss");
            string colored;
            if (message.Contains("CMD:") || message.Contains("COMMAND"))
                colored = $"<color=#4d5470>[{time}]</color> <color=#00e5ff><b>{message}</b></color>";
            else if (message.Contains("State:") || message.Contains("→"))
                colored = $"<color=#4d5470>[{time}]</color> <color=#ffab00>{message}</color>";
            else if (message.Contains("onnect"))
                colored = $"<color=#4d5470>[{time}]</color> <color=#00e676>{message}</color>";
            else if (message.Contains("rror") || message.Contains("ail"))
                colored = $"<color=#4d5470>[{time}]</color> <color=#ff3d57>{message}</color>";
            else
                colored = $"<color=#4d5470>[{time}]</color> {message}";

            _logEntries.Add(colored);
            if (_logEntries.Count > MaxLogEntries)
                _logEntries.RemoveAt(0);

            if (_logText != null)
                _logText.text = string.Join("\n", _logEntries);
        }

        // ───────────────────────────────────────────────────────────
        //  CLEANUP
        // ───────────────────────────────────────────────────────────

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
