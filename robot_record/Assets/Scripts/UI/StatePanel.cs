using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.Models;
using RobotMiddleware.Recording;

namespace RobotMiddleware.UI
{
    public class StatePanel : MonoBehaviour
    {
        private RecordingManager _recordingManager;
        private Image _stateDot;
        private Text _stateText;
        private Text _recordIdText;

        public void Initialize(RecordingManager recordingManager, Transform parent)
        {
            _recordingManager = recordingManager;

            // Root panel
            var panel = HUDTheme.CreateBorderPanel("StatePanel", parent, HUDTheme.BgSurface, HUDTheme.BorderDefault);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.offsetMin = new Vector2(0f, 0f);
            panelRect.offsetMax = new Vector2(-4f, 0f);

            var inner = panel.transform.GetChild(0);

            // Add vertical layout
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Section label
            var label = HUDTheme.CreateLabel("Label", inner, "STATE", 9);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 16f;

            // State row: dot + text
            var stateRow = new GameObject("StateRow", typeof(RectTransform));
            stateRow.transform.SetParent(inner, false);
            var rowLayout = stateRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            var rowLayoutEl = stateRow.AddComponent<LayoutElement>();
            rowLayoutEl.preferredHeight = 22f;

            // Dot
            _stateDot = HUDTheme.CreateDot("StateDot", stateRow.transform, HUDTheme.TextSecondary, 10f);
            var dotLayout = _stateDot.gameObject.AddComponent<LayoutElement>();
            dotLayout.preferredWidth = 10f;
            dotLayout.preferredHeight = 10f;

            // State name
            _stateText = HUDTheme.CreateValueText("StateText", stateRow.transform, "PENDING", 14);
            _stateText.color = HUDTheme.TextPrimary;
            _stateText.fontStyle = FontStyle.Bold;

            // Record ID row
            _recordIdText = HUDTheme.CreateValueText("RecordId", inner, "Record: \u2014", 11);
            _recordIdText.color = HUDTheme.TextSecondary;
            var ridLayout = _recordIdText.gameObject.AddComponent<LayoutElement>();
            ridLayout.preferredHeight = 18f;

            // Subscribe
            if (_recordingManager != null)
            {
                _recordingManager.OnStateChanged += OnStateChanged;
                Refresh();
            }
        }

        private void OnStateChanged(RecordingState newState)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_recordingManager == null) return;

            var state = _recordingManager.CurrentState;
            var color = HUDTheme.GetStateColor(state);
            var displayName = HUDTheme.GetStateDisplayName(state);

            if (_stateDot != null) _stateDot.color = color;
            if (_stateText != null) _stateText.text = displayName;

            string rid = string.IsNullOrEmpty(_recordingManager.RecordId)
                ? "\u2014"
                : _recordingManager.RecordId;
            if (_recordIdText != null) _recordIdText.text = $"Record: {rid}";
        }

        private void OnDestroy()
        {
            if (_recordingManager != null)
                _recordingManager.OnStateChanged -= OnStateChanged;
        }
    }
}
