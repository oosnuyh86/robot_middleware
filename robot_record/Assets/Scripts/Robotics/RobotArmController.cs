using UnityEngine;

namespace RobotMiddleware.Robotics
{
    /// <summary>
    /// Controls the UR10e robot arm by driving ArticulationBody joint targets.
    /// Finds the 6 revolute joints from the imported URDF hierarchy.
    /// </summary>
    public class RobotArmController : MonoBehaviour
    {
        [Header("Joint Configuration")]
        [SerializeField] private float jointStiffness = 10000f;
        [SerializeField] private float jointDamping = 1000f;
        [SerializeField] private float jointForceLimit = 1000f;
        [SerializeField] private float jointSpeed = 2f; // radians per second for interpolation

        private ArticulationBody[] _joints;
        private float[] _currentTargets = new float[6];
        private float[] _goalTargets = new float[6];
        private bool _initialized;

        // Joint names matching the UR10e URDF
        private static readonly string[] JointNames = {
            "shoulder_pan_joint",
            "shoulder_lift_joint",
            "elbow_joint",
            "wrist_1_joint",
            "wrist_2_joint",
            "wrist_3_joint"
        };

        public bool IsInitialized => _initialized;
        public float[] CurrentJointAngles => GetCurrentJoints();

        private void Awake()
        {
            FindJoints();
        }

        private void FindJoints()
        {
            _joints = new ArticulationBody[6];
            var allBodies = GetComponentsInChildren<ArticulationBody>();

            for (int i = 0; i < JointNames.Length; i++)
            {
                foreach (var body in allBodies)
                {
                    if (body.gameObject.name.Contains(JointNames[i]) ||
                        body.gameObject.name.Contains(JointNames[i].Replace("_joint", "")))
                    {
                        _joints[i] = body;
                        ConfigureJoint(body);
                        break;
                    }
                }

                if (_joints[i] == null)
                {
                    Debug.LogWarning($"[RobotArmController] Joint not found: {JointNames[i]}");
                }
            }

            _initialized = true;
            for (int i = 0; i < 6; i++)
            {
                if (_joints[i] == null)
                {
                    _initialized = false;
                    break;
                }
            }

            if (_initialized)
                Debug.Log("[RobotArmController] All 6 joints found and configured");
            else
                Debug.LogWarning("[RobotArmController] Some joints missing — robot may not move correctly");
        }

        private void ConfigureJoint(ArticulationBody body)
        {
            if (body.jointType != ArticulationJointType.RevoluteJoint) return;

            var drive = body.xDrive;
            drive.stiffness = jointStiffness;
            drive.damping = jointDamping;
            drive.forceLimit = jointForceLimit;
            body.xDrive = drive;
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;

            // Smooth interpolation toward goal targets
            for (int i = 0; i < 6; i++)
            {
                _currentTargets[i] = Mathf.MoveTowards(
                    _currentTargets[i],
                    _goalTargets[i],
                    jointSpeed * Time.fixedDeltaTime
                );
            }

            ApplyTargets(_currentTargets);
        }

        /// <summary>
        /// Set target joint angles (radians). Robot will interpolate smoothly.
        /// </summary>
        public void SetJointTargets(float[] angles)
        {
            if (angles == null || angles.Length != 6) return;
            for (int i = 0; i < 6; i++)
                _goalTargets[i] = angles[i];
        }

        /// <summary>
        /// Set joint angles immediately (no interpolation).
        /// </summary>
        public void SetJointTargetsImmediate(float[] angles)
        {
            if (angles == null || angles.Length != 6) return;
            for (int i = 0; i < 6; i++)
            {
                _goalTargets[i] = angles[i];
                _currentTargets[i] = angles[i];
            }
            ApplyTargets(angles);
        }

        /// <summary>
        /// Read current joint positions (radians).
        /// </summary>
        public float[] GetCurrentJoints()
        {
            float[] joints = new float[6];
            for (int i = 0; i < 6; i++)
            {
                if (_joints[i] != null)
                    joints[i] = _joints[i].jointPosition[0] * Mathf.Deg2Rad;
            }
            return joints;
        }

        /// <summary>
        /// Move end-effector to target pose using IK solver.
        /// </summary>
        public bool MoveToTarget(Vector3 targetPos, Quaternion targetRot)
        {
            float[][] solutions = UR10eIKSolver.SolveIK(targetPos, targetRot);
            if (solutions.Length == 0)
            {
                Debug.LogWarning("[RobotArmController] No IK solution found for target pose");
                return false;
            }

            float[] best = UR10eIKSolver.SelectBestSolution(solutions, GetCurrentJoints());
            if (best == null) return false;

            SetJointTargets(best);
            return true;
        }

        /// <summary>
        /// Get current end-effector pose via forward kinematics.
        /// </summary>
        public Matrix4x4 GetEndEffectorPose()
        {
            return UR10eIKSolver.ForwardKinematics(GetCurrentJoints());
        }

        private void ApplyTargets(float[] angles)
        {
            for (int i = 0; i < 6; i++)
            {
                if (_joints[i] == null) continue;

                var drive = _joints[i].xDrive;
                drive.target = angles[i] * Mathf.Rad2Deg; // ArticulationBody uses degrees
                _joints[i].xDrive = drive;
            }
        }
    }
}
