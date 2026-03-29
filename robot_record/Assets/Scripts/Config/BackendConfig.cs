using UnityEngine;

namespace RobotMiddleware.Config
{
    public class BackendConfig : MonoBehaviour
    {
        public enum Environment
        {
            Development,
            Staging,
            Production
        }

        private static BackendConfig _instance;

        public string BaseApiUrl { get; private set; }
        public string RelayUrl { get; private set; }
        // Backward-compat alias
        public string SignalingUrl => RelayUrl;
        public Environment CurrentEnvironment { get; private set; }

        public static BackendConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<BackendConfig>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("BackendConfig");
                        _instance = obj.AddComponent<BackendConfig>();
                    }
                    DontDestroyOnLoad(_instance.gameObject);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            SetEnvironment(Environment.Development);
        }

        public void SetEnvironment(Environment env)
        {
            CurrentEnvironment = env;

            switch (env)
            {
                case Environment.Development:
                    BaseApiUrl = "http://localhost:4000/api";
                    RelayUrl = "ws://localhost:4000/ws";
                    break;
                case Environment.Staging:
                    BaseApiUrl = "https://staging.example.com/api";
                    RelayUrl = "wss://staging.example.com/ws";
                    break;
                case Environment.Production:
                    BaseApiUrl = "https://api.example.com/api";
                    RelayUrl = "wss://api.example.com/ws";
                    break;
            }

            Debug.Log($"[BackendConfig] Environment set to {env}: {BaseApiUrl}");
        }
    }
}
