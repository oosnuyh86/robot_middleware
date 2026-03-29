using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.Models;

namespace RobotMiddleware.UI
{
    /// <summary>
    /// Static theme constants and factory helpers for the HUD.
    /// All values are pixel-matched to the reference design at 1920x1080.
    /// </summary>
    public static class HUDTheme
    {
        // ── Background colors ──
        public static readonly Color BgRoot     = HexColor(0x0a, 0x0c, 0x12);
        public static readonly Color BgSurface  = HexColor(0x11, 0x13, 0x1b);
        public static readonly Color BgElevated = HexColor(0x17, 0x1a, 0x25);
        public static readonly Color BgPanel    = HexColor(0x0e, 0x10, 0x19);
        public static readonly Color BgHover    = HexColor(0x1d, 0x20, 0x30);

        // ── Border colors ──
        public static readonly Color Border     = HexColor(0x25, 0x2a, 0x3a);
        public static readonly Color BorderGlow = HexColor(0x1a, 0x3a, 0x4a);

        // ── Text colors ──
        public static readonly Color Text1 = HexColor(0xe8, 0xea, 0xf0); // primary
        public static readonly Color Text2 = HexColor(0x7a, 0x80, 0x99); // secondary
        public static readonly Color Text3 = HexColor(0x4d, 0x54, 0x70); // labels

        // Aliases used by panel scripts
        public static readonly Color TextPrimary   = Text1;
        public static readonly Color TextSecondary = Text2;

        // ── Accent colors ──
        public static readonly Color Cyan    = HexColor(0x00, 0xe5, 0xff);
        public static readonly Color CyanDim = HexColor(0x00, 0xb8, 0xd4);
        public static readonly Color Red     = HexColor(0xff, 0x3d, 0x57);
        public static readonly Color Green   = HexColor(0x00, 0xe6, 0x76);
        public static readonly Color Yellow  = HexColor(0xff, 0xab, 0x00);
        public static readonly Color Purple  = HexColor(0xb3, 0x88, 0xff);
        public static readonly Color Blue    = HexColor(0x44, 0x8a, 0xff);
        public static readonly Color Orange  = HexColor(0xff, 0x91, 0x00);

        // Aliases used by panel scripts
        public static readonly Color AccentCyan    = Cyan;
        public static readonly Color DangerRed     = Red;
        public static readonly Color SuccessGreen  = Green;
        public static readonly Color WarningOrange = Yellow;
        public static readonly Color InfoBlue      = Blue;

        // ── Glow variants (for status indicator backgrounds) ──
        public static readonly Color CyanGlow  = new Color(0f, 0.898f, 1f, 0.08f);
        public static readonly Color CyanGlowS = new Color(0f, 0.898f, 1f, 0.18f);
        public static readonly Color GreenGlow = new Color(0f, 0.902f, 0.463f, 0.08f);
        public static readonly Color RedGlow   = new Color(1f, 0.239f, 0.341f, 0.08f);

        // ── Font size constants (px at 1920x1080) ──
        public const int FontXS  = 11;  // log entries, timestamps, panel labels
        public const int FontSM  = 13;  // secondary labels, record ID, sensor title
        public const int FontMD  = 16;  // flow unit
        public const int FontLG  = 20;  // tracker values
        public const int FontXL  = 28;  // state name
        public const int Font2XL = 32;  // flow value
        public const int FontHeaderTitle    = 15;
        public const int FontHeaderSubtitle = 13;
        public const int FontLogEntry       = 12;
        public const int FontFeedLabel      = 10;
        public const int FontTrackerAxis    = 13;
        public const int FontTrackerLabel   = 12;
        public const int FontTrackerStatus  = 14;
        public const int FontStatusIndicator = 12;

        // ── Spacing constants ──
        public const float SpXS  = 4f;
        public const float SpSM  = 8f;
        public const float SpMD  = 12f;
        public const float SpLG  = 16f;
        public const float SpXL  = 20f;
        public const float Sp2XL = 24f;
        public const float Sp3XL = 32f;

        // ── Layout constants ──
        public const float HeaderHeight      = 48f;
        public const float BottomRowHeight   = 140f;
        public const float StatePanelWidth   = 320f;
        public const float FlowPanelWidth    = 280f;
        public const float TrackerPanelHeight = 220f;
        public const float RootPadding       = 8f;
        public const float PanelGap          = 8f;
        public const float LeftColFlex       = 65f;
        public const float RightColFlex      = 35f;

        // ── Font ──
        private static Font _monoFont;
        public static Font MonoFont
        {
            get
            {
                if (_monoFont == null)
                {
                    _monoFont = Font.CreateDynamicFontFromOSFont("Consolas", 14);
                    if (_monoFont == null)
                        _monoFont = Font.CreateDynamicFontFromOSFont("Courier New", 14);
                }
                return _monoFont;
            }
        }

        // ── State color mapping ──
        public static Color GetStateColor(RecordingState state)
        {
            return state switch
            {
                RecordingState.Idle       => Text2,    // PENDING gray
                RecordingState.Scanning   => Blue,     // SCANNING blue
                RecordingState.Aligning   => Cyan,     // ALIGNING cyan
                RecordingState.Recording  => Red,      // RECORDING red
                RecordingState.Uploading  => Orange,   // TRAINING orange
                RecordingState.Training   => Orange,   // TRAINING orange
                RecordingState.Validating => Purple,   // VALIDATING purple
                RecordingState.Approved   => Purple,   // still validating phase
                RecordingState.Executing  => Green,    // EXECUTING green
                RecordingState.Complete   => Green,    // COMPLETED green
                RecordingState.Failed     => Red,      // FAILED red
                _                         => Text2
            };
        }

        /// <summary>Returns true if the state should show a glow/pulse animation.</summary>
        public static bool GetStateGlow(RecordingState state)
        {
            return state switch
            {
                RecordingState.Scanning   => true,
                RecordingState.Aligning   => true,
                RecordingState.Recording  => true,
                RecordingState.Uploading  => true,
                RecordingState.Training   => true,
                RecordingState.Validating => true,
                RecordingState.Approved   => true,
                RecordingState.Executing  => true,
                _                         => false
            };
        }

        public static string GetStateDisplayName(RecordingState state)
        {
            return state switch
            {
                RecordingState.Idle       => "PENDING",
                RecordingState.Scanning   => "SCANNING",
                RecordingState.Aligning   => "ALIGNING",
                RecordingState.Recording  => "RECORDING",
                RecordingState.Uploading  => "TRAINING",
                RecordingState.Training   => "TRAINING",
                RecordingState.Validating => "VALIDATING",
                RecordingState.Approved   => "VALIDATING",
                RecordingState.Executing  => "EXECUTING",
                RecordingState.Complete   => "COMPLETED",
                RecordingState.Failed     => "FAILED",
                _                         => "UNKNOWN"
            };
        }

        // ── Factory: Panel with background color ──
        public static GameObject CreatePanel(string name, Transform parent, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bgColor;
            return go;
        }

        /// <summary>
        /// Creates a panel with a 1px border simulated via an outer colored rect
        /// and an inset inner rect.
        /// </summary>
        public static GameObject CreateBorderPanel(string name, Transform parent, Color bgColor, Color borderColor, float borderWidth = 1f)
        {
            var outer = CreatePanel(name, parent, borderColor);

            var inner = CreatePanel(name + "_Inner", outer.transform, bgColor);
            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(borderWidth, borderWidth);
            innerRect.offsetMax = new Vector2(-borderWidth, -borderWidth);

            return outer;
        }

        /// <summary>
        /// Universal text factory. All HUD text goes through here.
        /// </summary>
        public static Text CreateText(string name, Transform parent, string text, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = MonoFont;
            t.fontSize = fontSize;
            t.color = color;
            t.text = text;
            t.alignment = alignment;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Creates a RawImage element (for render textures, camera feeds).</summary>
        public static RawImage CreateRawImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var ri = go.GetComponent<RawImage>();
            ri.color = Color.white;
            return ri;
        }

        /// <summary>Creates a circular dot indicator.</summary>
        public static Image CreateDot(string name, Transform parent, Color color, float size = 8f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>Simulate wide letter-spacing: "STATE" becomes "S T A T E".</summary>
        public static string SpaceOut(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var upper = input.ToUpperInvariant();
            return string.Join(" ", upper.ToCharArray());
        }

        /// <summary>Stretch a RectTransform to fill its parent.</summary>
        public static void StretchFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Set anchored position and size.</summary>
        public static void SetRect(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static Color HexColor(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
