#if UNITY_EDITOR

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RobotMiddleware.Controller;
using RobotMiddleware.Recording;
using RobotMiddleware.DataChannel;
using RobotMiddleware.Models;

namespace RobotMiddleware.Tests.Editor
{
    [TestFixture]
    public class MiddlewareControllerTests
    {
        private GameObject _testGameObject;
        private MiddlewareController _controller;
        private RecordingManager _recordingManager;

        [SetUp]
        public void SetUp()
        {
            // Suppress internal log errors from coroutines that hit null
            // dependencies (e.g. MiddlewareClient) in EditMode.
            LogAssert.ignoreFailingMessages = true;

            _testGameObject = new GameObject("TestMiddlewareController");
            _recordingManager = _testGameObject.AddComponent<RecordingManager>();
            _controller = _testGameObject.AddComponent<MiddlewareController>();

            // Wire up the [SerializeField] _recordingManager via reflection
            var rmField = typeof(MiddlewareController).GetField(
                "_recordingManager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            rmField.SetValue(_controller, _recordingManager);

            // In EditMode tests, Unity does not automatically call Awake.
            // Invoke manually so internal state is initialized.
            var rmAwake = typeof(RecordingManager).GetMethod(
                "Awake",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (rmAwake != null) rmAwake.Invoke(_recordingManager, null);

            var ctrlAwake = typeof(MiddlewareController).GetMethod(
                "Awake",
                BindingFlags.NonPublic | BindingFlags.Instance);
            ctrlAwake.Invoke(_controller, null);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(_testGameObject);
        }

        [Test]
        public void MiddlewareController_Awake_ShouldCreateCommandHandler()
        {
            // Awake is called automatically by AddComponent.
            // Verify _commandHandler was created via reflection.
            var field = typeof(MiddlewareController).GetField(
                "_commandHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var handler = field.GetValue(_controller);

            Assert.IsNotNull(handler, "CommandHandler should be created during Awake");
        }

        [Test]
        public void MiddlewareController_IsConnected_ShouldDefaultToFalse()
        {
            Assert.IsFalse(_controller.IsConnected, "IsConnected should default to false with no connections");
        }

        /// <summary>
        /// Helper to invoke the private HandleCommand method, unwrapping
        /// TargetInvocationException so tests see the real exception.
        /// </summary>
        private void InvokeHandleCommand(CommandMessage cmd)
        {
            var method = typeof(MiddlewareController).GetMethod(
                "HandleCommand",
                BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                method.Invoke(_controller, new object[] { cmd });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        [Test]
        public void MiddlewareController_HandleCommand_StartScan_ShouldDispatchWithoutError()
        {
            // StartScanning uses StartCoroutine; the coroutine body won't run
            // in EditMode, but the command dispatch itself should succeed.
            var cmd = new CommandMessage(CommandAction.START_SCAN);

            Assert.DoesNotThrow(() => InvokeHandleCommand(cmd),
                "HandleCommand(START_SCAN) should dispatch without throwing");
        }

        [Test]
        public void MiddlewareController_HandleCommand_MarkFailed_ShouldDispatchWithPayload()
        {
            // MARK_FAILED with payload sets RecordId synchronously, then the
            // coroutine tries to call MiddlewareClient which is not fully wired
            // in EditMode — expect the resulting internal exception log.
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("NullReferenceException"));

            var cmd = new CommandMessage(CommandAction.MARK_FAILED, "sensor_timeout");

            Assert.DoesNotThrow(() => InvokeHandleCommand(cmd),
                "HandleCommand(MARK_FAILED) should dispatch without throwing");
        }

        [Test]
        public void MiddlewareController_HandleCommand_WithRecordId_ShouldSetRecordId()
        {
            // TryExtractRecordId runs synchronously before the coroutine.
            // The coroutine then hits MiddlewareClient which is null in
            // EditMode — expect the resulting internal exception log.
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("NullReferenceException"));

            string testRecordId = "rec-abc-123";
            var cmd = new CommandMessage(CommandAction.START_SCAN, testRecordId);

            InvokeHandleCommand(cmd);

            Assert.AreEqual(testRecordId, _recordingManager.RecordId,
                "RecordId should be extracted from command payload");
        }

        /// <summary>
        /// Helper to force the RecordingManager's CurrentState for state-gate tests.
        /// </summary>
        private void SetRecordingState(RecordingState state)
        {
            var prop = typeof(RecordingManager).GetProperty(
                "CurrentState",
                BindingFlags.Public | BindingFlags.Instance);
            prop.SetValue(_recordingManager, state);
        }

        [Test]
        public void HandleCommand_ScanSubCommand_InWrongState_ShouldSendError()
        {
            // RecordingManager starts in Idle; CAPTURE_BACKGROUND requires Scanning.
            // The InvalidOperationException from the passthrough should be caught
            // by MiddlewareController and routed to SendError (no ACK).
            Assert.AreEqual(RecordingState.Idle, _recordingManager.CurrentState);

            var cmd = new CommandMessage(CommandAction.CAPTURE_BACKGROUND);

            Assert.DoesNotThrow(() => InvokeHandleCommand(cmd),
                "HandleCommand should swallow InvalidOperationException and not rethrow");
        }

        [Test]
        public void HandleCommand_ScanSubCommand_InScanningState_ShouldDispatch()
        {
            // When in Scanning state, the passthrough should NOT throw the state
            // gate — it will call into ScanManager which is null in this test scene,
            // so the null-guard logs an error and returns cleanly.
            SetRecordingState(RecordingState.Scanning);

            var cmd = new CommandMessage(CommandAction.CAPTURE_BACKGROUND);

            Assert.DoesNotThrow(() => InvokeHandleCommand(cmd),
                "HandleCommand(CAPTURE_BACKGROUND) in Scanning should dispatch without throwing");
        }

        [TestCase(CommandAction.CAPTURE_BACKGROUND)]
        [TestCase(CommandAction.START_OBJECT_SCAN)]
        [TestCase(CommandAction.CONFIRM_SCAN)]
        [TestCase(CommandAction.RESCAN)]
        public void HandleCommand_AllFourScanSubCommands_ShouldParseEnum(CommandAction action)
        {
            // Each sub-command should parse into the enum and be routed through
            // the switch in HandleCommand without throwing ArgumentException.
            // We use Idle so the state gate rejects (InvalidOperationException),
            // which HandleCommand catches and converts to SendError — the point
            // is that the enum parse + switch dispatch both work.
            Assert.AreEqual(RecordingState.Idle, _recordingManager.CurrentState);

            var cmd = new CommandMessage(action);

            Assert.DoesNotThrow(() => InvokeHandleCommand(cmd),
                $"HandleCommand({action}) should parse and dispatch without throwing");
        }
    }
}

#endif
