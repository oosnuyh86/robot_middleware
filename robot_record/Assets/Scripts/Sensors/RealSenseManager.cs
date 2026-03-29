using System;
using UnityEngine;

// SDK Integration Notes:
// ----------------------
// To integrate the real Intel RealSense SDK:
// 1. Clone or download the librealsense2 Unity wrapper from:
//    https://github.com/IntelRealSense/librealsense
// 2. Copy the "wrappers/unity" folder contents into your Assets/RealSense directory.
// 3. The wrapper provides RsDevice, RsProcessingPipe, and frame classes.
// 4. Replace the stub Update loop with:
//      using (var frames = _pipeline.WaitForFrames())
//      {
//          var colorFrame = frames.ColorFrame;
//          var depthFrame = frames.DepthFrame;
//          // Copy frame data into ColorTexture / DepthTexture
//      }
// 5. Alternatively, install via NuGet (Intel.RealSense) for the native bindings,
//    but the Unity wrapper above is recommended for Texture2D integration.

namespace RobotMiddleware.Sensors
{
    public class RealSenseManager : MonoBehaviour
    {
        public delegate void OnFrameReadyDelegate(Texture2D color, Texture2D depth);
        public event OnFrameReadyDelegate OnFrameReady;

        [SerializeField] private int _streamWidth = 640;
        [SerializeField] private int _streamHeight = 480;
        [SerializeField] private int _framerate = 30;

        public bool IsStreaming { get; private set; }
        public Texture2D ColorTexture { get; private set; }
        public Texture2D DepthTexture { get; private set; }

        private float _frameInterval;
        private float _timeSinceLastFrame;

        public void StartStreaming()
        {
            if (IsStreaming)
            {
                Debug.LogWarning("[RealSenseManager] Already streaming");
                return;
            }

            ColorTexture = new Texture2D(_streamWidth, _streamHeight, TextureFormat.RGB24, false);
            DepthTexture = new Texture2D(_streamWidth, _streamHeight, TextureFormat.R16, false);

            FillTextureSolid(ColorTexture, new Color(0.2f, 0.4f, 0.6f));
            FillTextureSolid(DepthTexture, new Color(0.5f, 0.5f, 0.5f));

            _frameInterval = 1f / _framerate;
            _timeSinceLastFrame = 0f;
            IsStreaming = true;

            Debug.Log($"[RealSenseManager] Streaming started ({_streamWidth}x{_streamHeight} @ {_framerate}fps)");
        }

        public void StopStreaming()
        {
            if (!IsStreaming)
            {
                Debug.LogWarning("[RealSenseManager] Not currently streaming");
                return;
            }

            IsStreaming = false;
            Debug.Log("[RealSenseManager] Streaming stopped");
        }

        private void Update()
        {
            if (!IsStreaming)
                return;

            _timeSinceLastFrame += Time.deltaTime;
            if (_timeSinceLastFrame < _frameInterval)
                return;

            _timeSinceLastFrame -= _frameInterval;

            // Stub: vary placeholder color over time to simulate a live feed
            float t = Mathf.PingPong(Time.time * 0.2f, 1f);
            FillTextureSolid(ColorTexture, Color.Lerp(new Color(0.2f, 0.4f, 0.6f), new Color(0.6f, 0.4f, 0.2f), t));
            FillTextureSolid(DepthTexture, new Color(t, t, t));

            OnFrameReady?.Invoke(ColorTexture, DepthTexture);
        }

        private void OnDestroy()
        {
            StopStreaming();
        }

        private static void FillTextureSolid(Texture2D tex, Color color)
        {
            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
        }
    }
}
