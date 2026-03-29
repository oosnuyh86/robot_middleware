using System.Collections;
using System.Linq;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RobotMiddleware.Recording;
using RobotMiddleware.Robotics;
using RobotMiddleware.Sensors;
using RobotMiddleware.Models;
using RobotMiddleware.ML;

namespace RobotMiddleware.Execution
{
    public class ExecutionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RecordingManager _recordingManager;
        [SerializeField] private RobotArmController _robotController;
        [SerializeField] private RobotArmAgent _robotAgent;
        [SerializeField] private FlowMeterManager _flowMeterManager;

        [Header("ROS Configuration")]
        [SerializeField] private ROSConfig _rosConfig;

        [Header("Safety")]
        [Tooltip("Maximum allowed joint velocity in rad/s. Set 0 to disable.")]
        [SerializeField] private float _maxJointVelocity = 3.14f;

        private ROSConnection _rosConnection;
        private Coroutine _publishCoroutine;
        private uint _sequenceCounter;
        private float[] _previousJointAngles;
        private bool _isPublishing;
        private bool _emergencyStopActive;

        private static readonly string[] JointNames = {
            "shoulder_pan_joint",
            "shoulder_lift_joint",
            "elbow_joint",
            "wrist_1_joint",
            "wrist_2_joint",
            "wrist_3_joint"
        };

        private void Awake()
        {
            if (_recordingManager == null)
                _recordingManager = FindAnyObjectByType<RecordingManager>();
            if (_robotController == null)
                _robotController = FindAnyObjectByType<RobotArmController>();
            if (_robotAgent == null)
                _robotAgent = FindAnyObjectByType<RobotArmAgent>();
            if (_flowMeterManager == null)
                _flowMeterManager = FindAnyObjectByType<FlowMeterManager>();
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

            StopPublishing();
        }

        private void HandleStateChanged(RecordingState newState)
        {
            if (newState == RecordingState.Executing)
                StartPublishing();
            else
                StopPublishing();
        }

        private void StartPublishing()
        {
            if (_isPublishing) return;

            if (_robotController == null || !_robotController.IsInitialized)
            {
                Debug.LogError("[ExecutionController] Cannot start: RobotArmController not initialized");
                _recordingManager?.MarkFailed("Robot arm controller not initialized");
                return;
            }

            _emergencyStopActive = false;
            _sequenceCounter = 0;
            _previousJointAngles = _robotController.GetCurrentJoints();

            // Initialize ROS connection
            ROSConfig config = _rosConfig != null ? _rosConfig : ROSConfig.CreateDefault();

            _rosConnection = ROSConnection.GetOrCreateInstance();
            _rosConnection.RegisterPublisher<JointStateMsg>(config.jointStateTopic);
            _rosConnection.RegisterPublisher<Float64MultiArrayMsg>(config.flowRateTopic);

            float interval = 1f / config.publishRateHz;
            _publishCoroutine = StartCoroutine(PublishLoop(interval, config));
            _isPublishing = true;

            Debug.Log($"[ExecutionController] Started publishing at {config.publishRateHz}Hz " +
                      $"on {config.jointStateTopic} and {config.flowRateTopic}");
        }

        private void StopPublishing()
        {
            if (!_isPublishing) return;

            if (_publishCoroutine != null)
            {
                StopCoroutine(_publishCoroutine);
                _publishCoroutine = null;
            }

            _isPublishing = false;
            Debug.Log("[ExecutionController] Stopped publishing");
        }

        private IEnumerator PublishLoop(float interval, ROSConfig config)
        {
            var wait = new WaitForSeconds(interval);

            while (true)
            {
                if (_emergencyStopActive)
                {
                    yield return wait;
                    continue;
                }

                float[] jointAngles = _robotController.GetCurrentJoints();

                // Safety: validate joint limits
                if (!UR10eIKSolver.ValidateSolution(jointAngles))
                {
                    Debug.LogError("[ExecutionController] Joint angles outside limits! Skipping publish.");
                    yield return wait;
                    continue;
                }

                // Safety: check joint velocity
                if (_maxJointVelocity > 0 && _previousJointAngles != null)
                {
                    bool velocityOk = true;
                    for (int i = 0; i < 6; i++)
                    {
                        float velocity = Mathf.Abs(jointAngles[i] - _previousJointAngles[i]) / interval;
                        if (velocity > _maxJointVelocity)
                        {
                            Debug.LogError($"[ExecutionController] Joint {i} velocity {velocity:F2} rad/s " +
                                           $"exceeds limit {_maxJointVelocity:F2} rad/s. Activating emergency stop.");
                            EmergencyStop();
                            velocityOk = false;
                            break;
                        }
                    }

                    if (!velocityOk)
                    {
                        yield return wait;
                        continue;
                    }
                }

                // Publish joint state
                PublishJointState(jointAngles, config.jointStateTopic);

                // Publish flow rate
                if (_flowMeterManager != null)
                    PublishFlowRate(_flowMeterManager.FlowRate, config.flowRateTopic);

                _previousJointAngles = (float[])jointAngles.Clone();

                yield return wait;
            }
        }

        private void PublishJointState(float[] jointAngles, string topic)
        {
            double timeSec = Time.timeAsDouble;
            uint sec = (uint)timeSec;
            uint nanosec = (uint)((timeSec - sec) * 1e9);

            var msg = new JointStateMsg
            {
                header = new RosMessageTypes.Std.HeaderMsg
                {
                    seq = _sequenceCounter++,
                    stamp = new RosMessageTypes.BuiltinInterfaces.TimeMsg(sec, nanosec),
                    frame_id = "base_link"
                },
                name = JointNames,
                position = jointAngles.Select(a => (double)a).ToArray(),
                velocity = new double[6],
                effort = new double[6]
            };

            _rosConnection.Publish(topic, msg);
        }

        private void PublishFlowRate(float flowRate, string topic)
        {
            var msg = new Float64MultiArrayMsg
            {
                layout = new MultiArrayLayoutMsg(),
                data = new double[] { flowRate }
            };

            _rosConnection.Publish(topic, msg);
        }

        /// <summary>
        /// Immediately stops all ROS publishing and logs an error.
        /// Can be called externally (e.g., from UI button or safety system).
        /// </summary>
        public void EmergencyStop()
        {
            _emergencyStopActive = true;
            Debug.LogWarning("[ExecutionController] EMERGENCY STOP activated — publishing halted");
        }

        /// <summary>
        /// Resets the emergency stop flag so publishing can resume.
        /// </summary>
        public void ResetEmergencyStop()
        {
            _emergencyStopActive = false;
            Debug.Log("[ExecutionController] Emergency stop reset");
        }

        public bool IsPublishing => _isPublishing;
        public bool IsEmergencyStopped => _emergencyStopActive;
        public uint PublishedMessageCount => _sequenceCounter;
    }
}
