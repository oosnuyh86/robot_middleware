using System;
using UnityEngine;

// SDK Integration Notes:
// ----------------------
// To wire a real Alicat flow meter via serial port:
//
// 1. Enable System.IO.Ports in Unity:
//    - In Player Settings > Other Settings > Api Compatibility Level, set to ".NET Framework"
//      (not .NET Standard) so that System.IO.Ports.SerialPort is available.
//
// 2. Replace the stub Update loop with a background thread reading from the serial port:
//      _serialPort = new System.IO.Ports.SerialPort(PortName, _baudRate);
//      _serialPort.Open();
//      // In a background thread or coroutine:
//      string line = _serialPort.ReadLine();
//      // Parse the Alicat ASCII response. Typical format:
//      //   "A +00050.0 +00100.0 +025.00 +100.00 Air"
//      //   Fields: unit ID, pressure, temperature, volumetric flow, mass flow, gas
//      float parsedFlow = ParseFlowFromResponse(line);
//
// 3. Alicat default serial settings: 19200 baud, 8N1, no flow control.
//    Send "A\r" to poll unit "A", or configure streaming mode on the device.
//
// 4. Marshal data back to the main thread using a ConcurrentQueue or
//    UnityMainThreadDispatcher before updating FlowRate and firing events.

namespace RobotMiddleware.Sensors
{
    public class FlowMeterManager : MonoBehaviour
    {
        public delegate void OnFlowRateUpdatedDelegate(float rate);
        public event OnFlowRateUpdatedDelegate OnFlowRateUpdated;

        [SerializeField] private string _portName = "COM3";
        [SerializeField] private int _baudRate = 19200;

        [Header("Stub Simulation Settings")]
        [SerializeField] private float _simulatedFlowMin;
        [SerializeField] private float _simulatedFlowMax = 100f;
        [SerializeField] private float _simulatedCycleSpeed = 1f;

        public float FlowRate { get; private set; }
        public bool IsConnected { get; private set; }
        public string PortName => _portName;

        public void Connect(string portName)
        {
            if (IsConnected)
            {
                Debug.LogWarning("[FlowMeterManager] Already connected");
                return;
            }

            _portName = portName;
            IsConnected = true;
            Debug.Log($"[FlowMeterManager] Connected to {_portName} @ {_baudRate} baud (stub)");
        }

        public void Disconnect()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[FlowMeterManager] Not connected");
                return;
            }

            IsConnected = false;
            FlowRate = 0f;
            Debug.Log("[FlowMeterManager] Disconnected");
        }

        private void Update()
        {
            if (!IsConnected)
                return;

            // Stub: simulate a sine wave flow rate for testing
            float t = Time.time * _simulatedCycleSpeed;
            float normalized = (Mathf.Sin(t) + 1f) * 0.5f; // 0..1
            FlowRate = Mathf.Lerp(_simulatedFlowMin, _simulatedFlowMax, normalized);

            OnFlowRateUpdated?.Invoke(FlowRate);
        }

        private void OnDestroy()
        {
            if (IsConnected)
                Disconnect();
        }
    }
}
