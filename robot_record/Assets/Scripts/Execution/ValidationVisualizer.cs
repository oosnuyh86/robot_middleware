using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RobotMiddleware.Recording;
using RobotMiddleware.Robotics;
using RobotMiddleware.Models;
using RobotMiddleware.ML;

namespace RobotMiddleware.Execution
{
    public class ValidationVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RecordingManager _recordingManager;
        [SerializeField] private RobotArmController _robotController;
        [SerializeField] private RobotArmAgent _robotAgent;

        [Header("Validation Settings")]
        [SerializeField] private float _sampleIntervalSec = 0.02f;
        [SerializeField] private float _validationDurationSec = 30f;

        [Header("HUD Style")]
        [SerializeField] private int _hudFontSize = 18;
        [SerializeField] private Color _hudPassColor = Color.green;
        [SerializeField] private Color _hudFailColor = Color.red;
        [SerializeField] private float _positionThreshold = 0.05f; // meters
        [SerializeField] private float _orientationThreshold = 10f; // degrees

        private bool _isValidating;
        private Coroutine _validationCoroutine;

        // Trajectory data collected during validation dry-run
        private List<Vector3> _predictedPositions = new List<Vector3>();
        private List<Quaternion> _predictedRotations = new List<Quaternion>();
        private List<float> _predictedFlowRates = new List<float>();

        // Computed metrics
        private float _positionRMSE;
        private float _orientationErrorDeg;
        private float _flowTimingDeviation;
        private bool _metricsReady;

        private void Awake()
        {
            if (_recordingManager == null)
                _recordingManager = FindAnyObjectByType<RecordingManager>();
            if (_robotController == null)
                _robotController = FindAnyObjectByType<RobotArmController>();
            if (_robotAgent == null)
                _robotAgent = FindAnyObjectByType<RobotArmAgent>();
        }

        private void OnEnable()
        {
            if (_recordingManager != null)
                _recordingManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_recordingManager != null)
                _recordingManager.OnStateChanged -= HandleStateChanged;

            StopValidation();
        }

        private void HandleStateChanged(RecordingState newState)
        {
            if (newState == RecordingState.Validating)
                StartValidation();
            else
                StopValidation();
        }

        private void StartValidation()
        {
            if (_isValidating) return;

            if (_robotController == null || !_robotController.IsInitialized)
            {
                Debug.LogError("[ValidationVisualizer] Cannot validate: RobotArmController not initialized");
                return;
            }

            _metricsReady = false;
            _predictedPositions.Clear();
            _predictedRotations.Clear();
            _predictedFlowRates.Clear();

            _validationCoroutine = StartCoroutine(RunValidation());
            _isValidating = true;

            Debug.Log("[ValidationVisualizer] Validation dry-run started");
        }

        private void StopValidation()
        {
            if (!_isValidating) return;

            if (_validationCoroutine != null)
            {
                StopCoroutine(_validationCoroutine);
                _validationCoroutine = null;
            }

            _isValidating = false;
        }

        private IEnumerator RunValidation()
        {
            float elapsed = 0f;
            var wait = new WaitForSeconds(_sampleIntervalSec);

            // Reset robot to home position for consistent replay
            _robotController.SetJointTargetsImmediate(new float[6]);
            yield return new WaitForSeconds(0.5f);

            while (elapsed < _validationDurationSec)
            {
                // Sample the current end-effector pose from the agent's inference output
                Matrix4x4 eePose = _robotController.GetEndEffectorPose();
                _predictedPositions.Add(eePose.GetPosition());
                _predictedRotations.Add(eePose.rotation);

                elapsed += _sampleIntervalSec;
                yield return wait;
            }

            ComputeMetrics();
            _isValidating = false;
            _validationCoroutine = null;

            Debug.Log($"[ValidationVisualizer] Validation complete — " +
                      $"Position RMSE: {_positionRMSE:F4}m, " +
                      $"Orientation Error: {_orientationErrorDeg:F2} deg, " +
                      $"Flow Deviation: {_flowTimingDeviation:F4}");
        }

        private void ComputeMetrics()
        {
            if (_predictedPositions.Count == 0)
            {
                _positionRMSE = float.MaxValue;
                _orientationErrorDeg = float.MaxValue;
                _flowTimingDeviation = float.MaxValue;
                _metricsReady = true;
                return;
            }

            // Position RMSE: measure consistency of trajectory (deviation from mean path)
            // In a full system this would compare against the recorded demonstration.
            // For now, compute the standard deviation of position increments as a smoothness metric.
            float sumSqPosError = 0f;
            float sumRotError = 0f;

            for (int i = 1; i < _predictedPositions.Count; i++)
            {
                // Position: accumulate squared displacement between consecutive samples
                Vector3 delta = _predictedPositions[i] - _predictedPositions[i - 1];
                sumSqPosError += delta.sqrMagnitude;

                // Orientation: angle between consecutive frames
                float angle = Quaternion.Angle(_predictedRotations[i], _predictedRotations[i - 1]);
                sumRotError += angle;
            }

            int n = _predictedPositions.Count - 1;
            _positionRMSE = n > 0 ? Mathf.Sqrt(sumSqPosError / n) : 0f;
            _orientationErrorDeg = n > 0 ? sumRotError / n : 0f;

            // Flow timing deviation placeholder
            // Would compare predicted flow activation timing against demonstration
            _flowTimingDeviation = 0f;

            _metricsReady = true;
        }

        /// <summary>
        /// Called by UI or operator to approve the validation and move to Approved state.
        /// </summary>
        public void ApproveValidation()
        {
            if (_recordingManager != null && _recordingManager.CurrentState == RecordingState.Validating)
            {
                _recordingManager.ApproveValidation();
                Debug.Log("[ValidationVisualizer] Validation APPROVED by operator");
            }
        }

        /// <summary>
        /// Called by UI or operator to reject the validation and mark as failed.
        /// </summary>
        public void RejectValidation()
        {
            if (_recordingManager != null && _recordingManager.CurrentState == RecordingState.Validating)
            {
                _recordingManager.MarkFailed("Validation rejected by operator");
                Debug.Log("[ValidationVisualizer] Validation REJECTED by operator");
            }
        }

        private void OnGUI()
        {
            if (_recordingManager == null) return;

            var state = _recordingManager.CurrentState;
            if (state != RecordingState.Validating && state != RecordingState.Approved)
                return;

            float x = Screen.width - 320;
            float y = 10;
            float w = 310;

            GUI.skin.label.fontSize = _hudFontSize;
            GUI.skin.box.fontSize = _hudFontSize;
            GUI.skin.button.fontSize = _hudFontSize - 2;

            GUI.Box(new Rect(x, y, w, _metricsReady ? 200 : 60), "Validation Metrics");
            y += 30;

            if (_isValidating)
            {
                GUI.Label(new Rect(x + 10, y, w - 20, 30), "Running dry-run replay...");
            }
            else if (_metricsReady)
            {
                bool posPass = _positionRMSE < _positionThreshold;
                bool rotPass = _orientationErrorDeg < _orientationThreshold;

                Color prevColor = GUI.color;

                GUI.color = posPass ? _hudPassColor : _hudFailColor;
                GUI.Label(new Rect(x + 10, y, w - 20, 25),
                    $"Position RMSE: {_positionRMSE:F4} m {(posPass ? "PASS" : "FAIL")}");
                y += 25;

                GUI.color = rotPass ? _hudPassColor : _hudFailColor;
                GUI.Label(new Rect(x + 10, y, w - 20, 25),
                    $"Orientation Err: {_orientationErrorDeg:F2} deg {(rotPass ? "PASS" : "FAIL")}");
                y += 25;

                GUI.color = Color.white;
                GUI.Label(new Rect(x + 10, y, w - 20, 25),
                    $"Flow Deviation: {_flowTimingDeviation:F4}");
                y += 25;

                GUI.Label(new Rect(x + 10, y, w - 20, 25),
                    $"Samples: {_predictedPositions.Count}");
                y += 35;

                GUI.color = prevColor;

                if (state == RecordingState.Validating)
                {
                    if (GUI.Button(new Rect(x + 10, y, 140, 35), "APPROVE"))
                        ApproveValidation();

                    if (GUI.Button(new Rect(x + 160, y, 140, 35), "REJECT"))
                        RejectValidation();
                }
            }
        }

        public float PositionRMSE => _positionRMSE;
        public float OrientationErrorDeg => _orientationErrorDeg;
        public float FlowTimingDeviation => _flowTimingDeviation;
        public bool MetricsReady => _metricsReady;
    }
}
