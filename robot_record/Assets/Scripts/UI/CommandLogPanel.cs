using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RobotMiddleware.Controller;

namespace RobotMiddleware.UI
{
    public class CommandLogPanel : MonoBehaviour
    {
        private const int MaxEntries = 20;
        private MiddlewareController _controller;
        private readonly List<string> _entries = new List<string>();
        private Text _logText;

        public void Initialize(MiddlewareController controller, Transform parent)
        {
            _controller = controller;

            // Root panel
            var panel = HUDTheme.CreateBorderPanel("CommandLogPanel", parent, HUDTheme.BgSurface, HUDTheme.BorderDefault);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var inner = panel.transform.GetChild(0);

            // Vertical layout for label + log text
            var vLayout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(10, 10, 6, 6);
            vLayout.spacing = 4f;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;

            // Label
            var label = HUDTheme.CreateLabel("LogLabel", inner, "COMMAND LOG", 9);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            // Log text fills remaining space
            _logText = HUDTheme.CreateValueText("LogEntries", inner, "", 10);
            _logText.color = HUDTheme.TextSecondary;
            _logText.verticalOverflow = VerticalWrapMode.Truncate;
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.alignment = TextAnchor.LowerLeft;
            var logLayout = _logText.gameObject.AddComponent<LayoutElement>();
            logLayout.flexibleHeight = 1f;

            // Subscribe
            if (_controller != null)
            {
                _controller.OnMessageReceived += msg => AddLog($"RX: {Truncate(msg, 70)}");
                _controller.OnStatusChanged += status => AddLog(status);
            }

            AddLog("HUD initialized");
        }

        public void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _entries.Add($"[{timestamp}] {message}");

            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);

            if (_logText != null)
                _logText.text = string.Join("\n", _entries);
        }

        private static string Truncate(string s, int max)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
