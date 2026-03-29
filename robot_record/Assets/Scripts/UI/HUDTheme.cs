using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.Models;

namespace RobotMiddleware.UI
{
    public static class HUDTheme
    {
        // ── Base colors (matching frontend globals.css exactly) ──
        public static readonly Color BgRoot      = new Color(0.039f, 0.047f, 0.071f, 1f);
        public static readonly Color BgSurface   = new Color(0.067f, 0.075f, 0.106f, 1f);
        public static readonly Color BgElevated  = new Color(0.090f, 0.102f, 0.145f, 1f);

        // Borders
        public static readonly Color BorderDefault = new Color(0.145f, 0.165f, 0.227f, 1f);

        // Text
        public static readonly Color TextPrimary   = new Color(0.910f, 0.918f, 0.941f, 1f);
        public static readonly Color TextSecondary  = new Color(0.478f, 0.502f, 0.600f, 1f);

        // Semantic
        public static readonly Color AccentCyan   = new Color(0f, 0.898f, 1f, 1f);
        public static readonly Color DangerRed    = new Color(1f, 0.239f, 0.341f, 1f);
        public static readonly Color SuccessGreen = new Color(0f, 0.902f, 0.463f, 1f);
        public static readonly Color WarningOrange = new Color(1f, 0.671f, 0f, 1f);
        public static readonly Color Purple       = new Color(0.702f, 0.533f, 1f, 1f);
        public static readonly Color InfoBlue     = new Color(0.267f, 0.541f, 1f, 1f);
        public static readonly Color TrainingOrange = new Color(1f, 0.569f, 0f, 1f); // #ff9100

        // Fonts
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
                RecordingState.Idle       => TextSecondary,    // PENDING gray
                RecordingState.Scanning   => InfoBlue,         // SCANNING blue
                RecordingState.Aligning   => AccentCyan,       // ALIGNING cyan
                RecordingState.Recording  => DangerRed,        // RECORDING red
                RecordingState.Uploading  => TrainingOrange,   // TRAINING orange
                RecordingState.Training   => TrainingOrange,   // TRAINING orange
                RecordingState.Validating => Purple,           // VALIDATING purple
                RecordingState.Approved   => Purple,           // still validating phase
                RecordingState.Executing  => SuccessGreen,     // EXECUTING green
                RecordingState.Complete   => SuccessGreen,     // COMPLETED green
                RecordingState.Failed     => DangerRed,        // FAILED red
                _                         => TextSecondary
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

        // ── Factory helpers ──

        public static GameObject CreatePanel(string name, Transform parent, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bgColor;
            return go;
        }

        public static GameObject CreateBorderPanel(string name, Transform parent, Color bgColor, Color borderColor, float borderWidth = 1f)
        {
            // Outer panel acts as border
            var outer = CreatePanel(name, parent, borderColor);
            var outerRect = outer.GetComponent<RectTransform>();

            // Inner panel is the actual background
            var inner = CreatePanel(name + "_Inner", outer.transform, bgColor);
            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(borderWidth, borderWidth);
            innerRect.offsetMax = new Vector2(-borderWidth, -borderWidth);

            return outer;
        }

        public static Text CreateLabel(string name, Transform parent, string text, int fontSize = 10)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = MonoFont;
            t.fontSize = fontSize;
            t.color = TextSecondary;
            t.text = SpaceOut(text);
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text CreateValueText(string name, Transform parent, string text, int fontSize = 13)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = MonoFont;
            t.fontSize = fontSize;
            t.color = TextPrimary;
            t.text = text;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Image CreateDot(string name, Transform parent, Color color, float size = 8f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;

            // Make it circular by using a small sprite-less image
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);

            return img;
        }

        // Simulate wide letter-spacing for uppercase labels: "STATE" -> "S T A T E"
        public static string SpaceOut(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var upper = input.ToUpperInvariant();
            return string.Join(" ", upper.ToCharArray());
        }

        // Stretch a RectTransform to fill its parent
        public static void StretchFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Set anchored position and size
        public static void SetRect(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
        }
    }
}
