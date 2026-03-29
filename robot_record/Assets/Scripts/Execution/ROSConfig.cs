using UnityEngine;

namespace RobotMiddleware.Execution
{
    [CreateAssetMenu(fileName = "ROSConfig", menuName = "RobotMiddleware/ROS Config")]
    public class ROSConfig : ScriptableObject
    {
        [Header("ROS Bridge Connection")]
        public string rosBridgeIP = "127.0.0.1";
        public int rosBridgePort = 10000;

        [Header("Topic Names")]
        public string jointStateTopic = "/joint_states";
        public string flowRateTopic = "/flow_rate";

        [Header("Publishing")]
        [Tooltip("Publishing rate in Hz. UR10e control rate is 125Hz.")]
        public int publishRateHz = 125;

        public static ROSConfig CreateDefault()
        {
            var config = CreateInstance<ROSConfig>();
            config.rosBridgeIP = "127.0.0.1";
            config.rosBridgePort = 10000;
            config.jointStateTopic = "/joint_states";
            config.flowRateTopic = "/flow_rate";
            config.publishRateHz = 125;
            return config;
        }
    }
}
