using System;
using UnityEngine;
#if REALSENSE_SDK
using Intel.RealSense;
#endif

// SDK Setup Instructions:
// -----------------------
// 1. Download Intel.RealSense.unitypackage from:
//    https://github.com/IntelRealSense/librealsense/releases
// 2. Import via Assets > Import Package > Custom Package
// 3. Add REALSENSE_SDK to Project Settings > Player > Scripting Define Symbols
// 4. The package bundles realsense2.dll (native) + C# wrappers automatically.
// Without the SDK imported, this class runs in stub mode with placeholder textures.

namespace RobotMiddleware.Sensors
{
    [Serializable]
    public struct DepthIntrinsics
    {
        public float fx;
        public float fy;
        public float cx;
        public float cy;
        public int width;
        public int height;
    }

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

#if REALSENSE_SDK
        private bool _stubMode;
        private Pipeline _pipeline;
        private PipelineProfile _pipelineProfile;
        private byte[] _colorData;
        private byte[] _depthRawBytes;
        private float _depthScale;
        private DepthIntrinsics _cachedIntrinsics;
        private bool _intrinsicsCached;
        private ushort[] _depthDataCache;
#endif

        public void StartStreaming()
        {
            if (IsStreaming)
            {
                Debug.LogWarning("[RealSenseManager] Already streaming");
                return;
            }

            ColorTexture = new Texture2D(_streamWidth, _streamHeight, TextureFormat.RGB24, false);
            DepthTexture = new Texture2D(_streamWidth, _streamHeight, TextureFormat.R16, false);

#if REALSENSE_SDK
            try
            {
                _pipeline = new Pipeline();
                var cfg = new Config();
                cfg.EnableStream(Stream.Color, _streamWidth, _streamHeight, Format.Rgb8, _framerate);
                cfg.EnableStream(Stream.Depth, _streamWidth, _streamHeight, Format.Z16, _framerate);

                _pipelineProfile = _pipeline.Start(cfg);

                _colorData = new byte[_streamWidth * _streamHeight * 3]; // RGB8
                _depthRawBytes = new byte[_streamWidth * _streamHeight * 2]; // Z16 = 2 bytes per pixel
                _depthDataCache = new ushort[_streamWidth * _streamHeight];

                // Cache depth scale for manual depth lookups
                var depthSensor = _pipelineProfile.Device.QuerySensors()[0];
                _depthScale = depthSensor.DepthScale;

                _stubMode = false;
                IsStreaming = true;
                Debug.Log($"[RealSenseManager] RealSense streaming started ({_streamWidth}x{_streamHeight} @ {_framerate}fps)");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RealSenseManager] Failed to start RealSense pipeline: {ex.Message}");
                Debug.LogWarning("[RealSenseManager] Falling back to stub mode");
                DisposePipeline();
                StartStubMode();
            }
#else
            StartStubMode();
#endif
        }

        public void StopStreaming()
        {
            if (!IsStreaming)
            {
                Debug.LogWarning("[RealSenseManager] Not currently streaming");
                return;
            }

#if REALSENSE_SDK
            if (!_stubMode)
                DisposePipeline();
#endif

            IsStreaming = false;
            Debug.Log("[RealSenseManager] Streaming stopped");
        }

        /// <summary>
        /// Returns the depth scale (meters per raw ushort unit).
        /// Returns 0 if not streaming or in stub mode.
        /// </summary>
        public float DepthScale
        {
            get
            {
#if REALSENSE_SDK
                if (!IsStreaming || _stubMode) return 0f;
                return _depthScale;
#else
                return 0f;
#endif
            }
        }

        /// <summary>
        /// Copies the current raw depth data (ushort per pixel, Z16) into the provided array.
        /// Array must be at least StreamWidth * StreamHeight in length.
        /// Returns false if not streaming, in stub mode, or data is unavailable.
        /// </summary>
        public bool CopyDepthData(ushort[] dest)
        {
#if REALSENSE_SDK
            if (!IsStreaming || _stubMode || _depthDataCache == null || dest == null)
                return false;
            if (dest.Length < _streamWidth * _streamHeight)
                return false;
            Array.Copy(_depthDataCache, dest, _streamWidth * _streamHeight);
            return true;
#else
            return false;
#endif
        }

        public int StreamWidth => _streamWidth;
        public int StreamHeight => _streamHeight;

        /// <summary>
        /// Returns depth in meters at the given pixel coordinate.
        /// Returns 0 if not streaming, in stub mode, or coordinates are out of range.
        /// </summary>
        public float GetDepthAtPixel(int x, int y)
        {
#if REALSENSE_SDK
            if (!IsStreaming || _stubMode || _depthDataCache == null)
                return 0f;
            if (x < 0 || x >= _streamWidth || y < 0 || y >= _streamHeight)
                return 0f;

            ushort raw = _depthDataCache[y * _streamWidth + x];
            return raw * _depthScale;
#else
            return 0f;
#endif
        }

        /// <summary>
        /// Returns the depth camera intrinsics (focal length, principal point, resolution).
        /// Returns zeroed struct if not streaming or in stub mode.
        /// </summary>
        public DepthIntrinsics GetDepthIntrinsics()
        {
#if REALSENSE_SDK
            if (!IsStreaming || _stubMode || _pipelineProfile == null)
                return default;

            if (!_intrinsicsCached)
            {
                try
                {
                    var depthStream = _pipelineProfile.GetStream<VideoStreamProfile>(Stream.Depth);
                    var intr = depthStream.GetIntrinsics();
                    _cachedIntrinsics = new DepthIntrinsics
                    {
                        fx = intr.fx,
                        fy = intr.fy,
                        cx = intr.ppx,
                        cy = intr.ppy,
                        width = intr.width,
                        height = intr.height
                    };
                    _intrinsicsCached = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RealSenseManager] Failed to get intrinsics: {ex.Message}");
                    return default;
                }
            }

            return _cachedIntrinsics;
#else
            return default;
#endif
        }

        private void Update()
        {
            if (!IsStreaming)
                return;

#if REALSENSE_SDK
            if (_stubMode)
            {
                UpdateStub();
                return;
            }

            try
            {
                FrameSet frames;
                if (!_pipeline.TryWaitForFrames(out frames, 50))
                    return; // No frame ready yet, avoid blocking the main thread

                using (frames)
                {
                    using (var colorFrame = frames.ColorFrame)
                    {
                        if (colorFrame != null)
                        {
                            colorFrame.CopyTo(_colorData);
                            ColorTexture.LoadRawTextureData(_colorData);
                            ColorTexture.Apply();
                        }
                    }

                    using (var depthFrame = frames.DepthFrame)
                    {
                        if (depthFrame != null)
                        {
                            depthFrame.CopyTo(_depthRawBytes);
                            DepthTexture.LoadRawTextureData(_depthRawBytes);
                            DepthTexture.Apply();

                            // Cache ushort depth data for GetDepthAtPixel
                            depthFrame.CopyTo(_depthDataCache);
                        }
                    }
                }

                OnFrameReady?.Invoke(ColorTexture, DepthTexture);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealSenseManager] Frame error: {ex.Message}");
            }
#else
            UpdateStub();
#endif
        }

        private void OnDestroy()
        {
            if (IsStreaming)
                StopStreaming();
        }

        // --- Stub mode (no SDK or no camera) ---

        private void StartStubMode()
        {
#if REALSENSE_SDK
            _stubMode = true;
#endif
            FillTextureSolid(ColorTexture, new Color(0.2f, 0.4f, 0.6f));
            FillTextureSolid(DepthTexture, new Color(0.5f, 0.5f, 0.5f));

            IsStreaming = true;
            Debug.Log($"[RealSenseManager] Stub mode started ({_streamWidth}x{_streamHeight} @ {_framerate}fps)");
        }

        private void UpdateStub()
        {
            float t = Mathf.PingPong(Time.time * 0.2f, 1f);
            FillTextureSolid(ColorTexture, Color.Lerp(new Color(0.2f, 0.4f, 0.6f), new Color(0.6f, 0.4f, 0.2f), t));
            FillTextureSolid(DepthTexture, new Color(t, t, t));
            OnFrameReady?.Invoke(ColorTexture, DepthTexture);
        }

#if REALSENSE_SDK
        private void DisposePipeline()
        {
            try
            {
                _pipeline?.Stop();
            }
            catch (Exception) { }

            _pipelineProfile?.Dispose();
            _pipeline?.Dispose();
            _pipeline = null;
            _pipelineProfile = null;
            _intrinsicsCached = false;
        }
#endif

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
