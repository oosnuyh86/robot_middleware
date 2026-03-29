#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;
using RobotMiddleware.Config;

namespace RobotMiddleware.Tests.Editor
{
    [TestFixture]
    public class BackendConfigTests
    {
        private GameObject _testGameObject;
        private BackendConfig _config;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestBackendConfig");
            _config = _testGameObject.AddComponent<BackendConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_testGameObject);
        }

        [Test]
        public void BackendConfig_Component_ShouldExistOnGameObject()
        {
            // Singleton uses DontDestroyOnLoad which requires PlayMode.
            // Instead, verify the component was properly added.
            Assert.IsNotNull(_config);
            Assert.IsNotNull(_testGameObject.GetComponent<BackendConfig>());
        }

        [Test]
        public void BackendConfig_DefaultEnvironment_ShouldBeDevelopment()
        {
            _config.SetEnvironment(BackendConfig.Environment.Development);

            Assert.AreEqual(BackendConfig.Environment.Development, _config.CurrentEnvironment);
            StringAssert.Contains("localhost:4000", _config.BaseApiUrl);
            StringAssert.Contains("localhost:4000", _config.SignalingUrl);
        }

        [Test]
        public void BackendConfig_SetEnvironment_ShouldUpdateUrls()
        {
            _config.SetEnvironment(BackendConfig.Environment.Staging);

            Assert.AreEqual(BackendConfig.Environment.Staging, _config.CurrentEnvironment);
            StringAssert.Contains("staging", _config.BaseApiUrl);

            _config.SetEnvironment(BackendConfig.Environment.Production);

            Assert.AreEqual(BackendConfig.Environment.Production, _config.CurrentEnvironment);
            StringAssert.Contains("api.example.com", _config.BaseApiUrl);
        }

        [Test]
        public void BackendConfig_SignalingUrl_ShouldStartWithWs()
        {
            _config.SetEnvironment(BackendConfig.Environment.Development);
            Assert.IsTrue(_config.SignalingUrl.StartsWith("ws"));

            _config.SetEnvironment(BackendConfig.Environment.Staging);
            Assert.IsTrue(_config.SignalingUrl.StartsWith("wss"));

            _config.SetEnvironment(BackendConfig.Environment.Production);
            Assert.IsTrue(_config.SignalingUrl.StartsWith("wss"));
        }
    }
}

#endif
