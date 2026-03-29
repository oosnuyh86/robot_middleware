using System;
using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.UI;

namespace RobotMiddleware.Calibration
{
    public class CalibrationUI : MonoBehaviour
    {
        [SerializeField] private AlignmentManager _alignmentManager;
        [SerializeField] private Canvas _parentCanvas;

        private GameObject _panel;
        private Text _titleText;
        private Text _instructionText;
        private Text _progressText;
        private Text _resultText;
        private Image[] _pointDots;
        private GameObject _captureButton;
        private GameObject _retryButton;
        private Text _captureButtonText;

        private bool _isComputing;
        private float _spinnerAngle;

        private void OnEnable()
        {
            if (_alignmentManager != null)
            {
                _alignmentManager.OnCalibrationProgress += HandleProgress;
                _alignmentManager.OnCalibrationComplete += HandleComplete;
                _alignmentManager.OnCalibrationFailed += HandleFailed;
            }
        }

        private void OnDisable()
        {
            if (_alignmentManager != null)
            {
                _alignmentManager.OnCalibrationProgress -= HandleProgress;
                _alignmentManager.OnCalibrationComplete -= HandleComplete;
                _alignmentManager.OnCalibrationFailed -= HandleFailed;
            }
        }

        private void Start()
        {
            BuildUI();
            SetState(UIState.Idle);
        }

        private void Update()
        {
            if (_isComputing)
            {
                _spinnerAngle += 180f * Time.deltaTime;
                if (_progressText != null)
                {
                    int dots = ((int)(_spinnerAngle / 60f)) % 4;
                    _progressText.text = "Computing alignment" + new string('.', dots);
                }
            }
        }

        // ── UI Construction ──

        private void BuildUI()
        {
            Transform parent = _parentCanvas != null ? _parentCanvas.transform : transform;

            // Main panel
            _panel = HUDTheme.CreateBorderPanel("CalibrationPanel", parent,
                HUDTheme.BgSurface, HUDTheme.BorderGlow);
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(420f, 320f);
            panelRect.anchoredPosition = Vector2.zero;

            Transform inner = _panel.transform.GetChild(0);

            // Title
            _titleText = HUDTheme.CreateText("Title", inner,
                HUDTheme.SpaceOut("Calibration"), HUDTheme.FontXL,
                HUDTheme.Cyan, TextAnchor.MiddleCenter, true);
            var titleRect = _titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -HUDTheme.SpMD);
            titleRect.sizeDelta = new Vector2(-HUDTheme.SpLG * 2, 36f);

            // Instruction text
            _instructionText = HUDTheme.CreateText("Instruction", inner,
                "", HUDTheme.FontSM, HUDTheme.TextSecondary, TextAnchor.MiddleCenter);
            var instrRect = _instructionText.GetComponent<RectTransform>();
            instrRect.anchorMin = new Vector2(0f, 1f);
            instrRect.anchorMax = new Vector2(1f, 1f);
            instrRect.pivot = new Vector2(0.5f, 1f);
            instrRect.anchoredPosition = new Vector2(0f, -56f);
            instrRect.sizeDelta = new Vector2(-HUDTheme.SpLG * 2, 24f);

            // Point indicator dots
            _pointDots = new Image[4];
            float dotStartX = -(4 * 24f - 16f) / 2f;
            for (int i = 0; i < 4; i++)
            {
                _pointDots[i] = HUDTheme.CreateDot($"Dot_{i}", inner, HUDTheme.Text3, 16f);
                var dotRect = _pointDots[i].GetComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0.5f, 1f);
                dotRect.anchorMax = new Vector2(0.5f, 1f);
                dotRect.pivot = new Vector2(0.5f, 1f);
                dotRect.anchoredPosition = new Vector2(dotStartX + i * 24f, -96f);
            }

            // Progress text
            _progressText = HUDTheme.CreateText("Progress", inner,
                "", HUDTheme.FontMD, HUDTheme.TextPrimary, TextAnchor.MiddleCenter);
            var progRect = _progressText.GetComponent<RectTransform>();
            progRect.anchorMin = new Vector2(0f, 1f);
            progRect.anchorMax = new Vector2(1f, 1f);
            progRect.pivot = new Vector2(0.5f, 1f);
            progRect.anchoredPosition = new Vector2(0f, -132f);
            progRect.sizeDelta = new Vector2(-HUDTheme.SpLG * 2, 28f);

            // Result text
            _resultText = HUDTheme.CreateText("Result", inner,
                "", HUDTheme.FontMD, HUDTheme.SuccessGreen, TextAnchor.MiddleCenter);
            var resRect = _resultText.GetComponent<RectTransform>();
            resRect.anchorMin = new Vector2(0f, 1f);
            resRect.anchorMax = new Vector2(1f, 1f);
            resRect.pivot = new Vector2(0.5f, 1f);
            resRect.anchoredPosition = new Vector2(0f, -172f);
            resRect.sizeDelta = new Vector2(-HUDTheme.SpLG * 2, 28f);

            // Capture button
            _captureButton = CreateButton("CaptureBtn", inner, "Capture Point",
                HUDTheme.Cyan, new Vector2(0f, -240f), OnCaptureClicked);
            _captureButtonText = _captureButton.GetComponentInChildren<Text>();

            // Retry button
            _retryButton = CreateButton("RetryBtn", inner, "Retry",
                HUDTheme.Yellow, new Vector2(0f, -280f), OnRetryClicked);
            _retryButton.SetActive(false);
        }

        private GameObject CreateButton(string name, Transform parent, string label,
            Color accentColor, Vector2 position, Action onClick)
        {
            var bg = HUDTheme.CreatePanel(name, parent, accentColor);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 1f);
            bgRect.anchorMax = new Vector2(0.5f, 1f);
            bgRect.pivot = new Vector2(0.5f, 1f);
            bgRect.anchoredPosition = position;
            bgRect.sizeDelta = new Vector2(180f, 32f);

            var btnText = HUDTheme.CreateText(name + "_Label", bg.transform, label,
                HUDTheme.FontSM, HUDTheme.BgRoot, TextAnchor.MiddleCenter, true);
            HUDTheme.StretchFill(btnText.GetComponent<RectTransform>());

            var button = bg.AddComponent<Button>();
            button.targetGraphic = bg.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());

            return bg;
        }

        // ── State Management ──

        private enum UIState { Idle, Capturing, Computing, Complete, Failed }

        private void SetState(UIState state, string detail = null)
        {
            _isComputing = false;
            _captureButton.SetActive(false);
            _retryButton.SetActive(false);
            _resultText.text = "";
            _progressText.text = "";

            switch (state)
            {
                case UIState.Idle:
                    _instructionText.text = "Press Capture to begin calibration";
                    _captureButton.SetActive(true);
                    _captureButtonText.text = "Start";
                    ResetDots();
                    break;

                case UIState.Capturing:
                    int current = _alignmentManager != null ? _alignmentManager.CapturedPointCount : 0;
                    int total = _alignmentManager != null ? _alignmentManager.RequiredPoints : 4;
                    _instructionText.text = $"Place tracker at position {current + 1}";
                    _progressText.text = $"Capturing point {current + 1}/{total}...";
                    _captureButton.SetActive(true);
                    _captureButtonText.text = "Capture Point";
                    break;

                case UIState.Computing:
                    _instructionText.text = "Analyzing spatial relationship...";
                    _isComputing = true;
                    break;

                case UIState.Complete:
                    _instructionText.text = "Calibration successful";
                    float conf = _alignmentManager != null ? _alignmentManager.LastConfidence : 0f;
                    _resultText.color = conf >= 0.7f ? HUDTheme.SuccessGreen : HUDTheme.WarningOrange;
                    _resultText.text = $"Alignment complete! Confidence: {conf:P1}";
                    if (conf < 0.7f)
                    {
                        _retryButton.SetActive(true);
                    }
                    break;

                case UIState.Failed:
                    _instructionText.text = "Calibration failed";
                    _resultText.color = HUDTheme.DangerRed;
                    _resultText.text = detail ?? "Unknown error";
                    _retryButton.SetActive(true);
                    ResetDots();
                    break;
            }
        }

        private void ResetDots()
        {
            if (_pointDots == null) return;
            for (int i = 0; i < _pointDots.Length; i++)
            {
                _pointDots[i].color = HUDTheme.Text3;
            }
        }

        private void UpdateDots(int captured)
        {
            if (_pointDots == null) return;
            for (int i = 0; i < _pointDots.Length; i++)
            {
                _pointDots[i].color = i < captured ? HUDTheme.SuccessGreen : HUDTheme.Text3;
            }
        }

        // ── Event Handlers ──

        private void HandleProgress(int captured, int total)
        {
            UpdateDots(captured);

            if (captured >= total)
            {
                SetState(UIState.Computing);
            }
            else
            {
                SetState(UIState.Capturing);
            }
        }

        private void HandleComplete(Matrix4x4 transform, float confidence)
        {
            SetState(UIState.Complete);
        }

        private void HandleFailed(string error)
        {
            SetState(UIState.Failed, error);
        }

        private void OnCaptureClicked()
        {
            if (_alignmentManager == null) return;

            if (!_alignmentManager.IsCalibrating)
            {
                _alignmentManager.StartCalibration();
                SetState(UIState.Capturing);
            }
            else
            {
                _alignmentManager.CapturePoint();
            }
        }

        private void OnRetryClicked()
        {
            if (_alignmentManager == null) return;

            _alignmentManager.CancelCalibration();
            ResetDots();
            _alignmentManager.StartCalibration();
            SetState(UIState.Capturing);
        }
    }
}
