using UnityEngine;
using Unity.MLAgents.Demonstrations;
using RobotMiddleware.Recording;
using RobotMiddleware.Models;

namespace RobotMiddleware.ML
{
    /// <summary>
    /// Manages ML-Agents demonstration recording for the spray painting task.
    /// Controlled by RecordingManager state machine:
    ///   - RECORDING state → starts demo recording (Heuristic mode)
    ///   - Other states → stops recording
    ///
    /// Demonstration files are saved to Assets/Demonstrations/ as .demo files.
    /// These are used by mlagents-learn for behavioral cloning / GAIL training.
    /// </summary>
    public class DemonstrationManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RobotArmAgent _agent;
        [SerializeField] private RecordingManager _recordingManager;

        [Header("Configuration")]
        [SerializeField] private string _demonstrationDirectory = "Assets/Demonstrations";
        [SerializeField] private string _demonstrationPrefix = "spray_painting";

        private DemonstrationRecorder _recorder;
        private bool _isRecording;
        private int _demoCount;

        public bool IsRecording => _isRecording;

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<RobotArmAgent>();
            if (_recordingManager == null)
                _recordingManager = FindAnyObjectByType<RecordingManager>();
        }

        private void OnEnable()
        {
            if (_recordingManager != null)
                _recordingManager.OnStateChanged += OnRecordingStateChanged;
        }

        private void OnDisable()
        {
            if (_recordingManager != null)
                _recordingManager.OnStateChanged -= OnRecordingStateChanged;
        }

        private void OnRecordingStateChanged(RecordingState newState)
        {
            if (newState == RecordingState.Recording)
            {
                StartDemoRecording();
            }
            else if (_isRecording)
            {
                StopDemoRecording();
            }
        }

        /// <summary>
        /// Start recording a new demonstration.
        /// Adds a DemonstrationRecorder component to the agent's GameObject.
        /// </summary>
        public void StartDemoRecording()
        {
            if (_isRecording)
            {
                Debug.LogWarning("[DemonstrationManager] Already recording");
                return;
            }

            if (_agent == null)
            {
                Debug.LogError("[DemonstrationManager] No RobotArmAgent assigned");
                return;
            }

            // Create a unique demonstration name
            _demoCount++;
            string demoName = $"{_demonstrationPrefix}_{System.DateTime.Now:yyyyMMdd_HHmmss}_{_demoCount:D3}";

            // Add DemonstrationRecorder to the agent's GameObject
            _recorder = _agent.gameObject.AddComponent<DemonstrationRecorder>();
            _recorder.DemonstrationName = demoName;
            _recorder.DemonstrationDirectory = _demonstrationDirectory;
            _recorder.Record = true;

            _isRecording = true;
            Debug.Log($"[DemonstrationManager] Recording started: {demoName}");
        }

        /// <summary>
        /// Stop the current demonstration recording.
        /// </summary>
        public void StopDemoRecording()
        {
            if (!_isRecording || _recorder == null)
                return;

            _recorder.Record = false;

            // Remove the recorder component (it has already flushed the .demo file)
            Destroy(_recorder);
            _recorder = null;

            _isRecording = false;
            Debug.Log("[DemonstrationManager] Recording stopped. Demo file saved.");
        }
    }
}
