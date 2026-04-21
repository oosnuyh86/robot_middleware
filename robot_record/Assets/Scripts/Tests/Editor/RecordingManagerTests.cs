#if UNITY_EDITOR

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RobotMiddleware.Recording;
using RobotMiddleware.Models;

namespace RobotMiddleware.Tests.Editor
{
    [TestFixture]
    public class RecordingManagerTests
    {
        private GameObject _testGameObject;
        private RecordingManager _manager;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestRecordingManager");
            _manager = _testGameObject.AddComponent<RecordingManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_testGameObject);
        }

        private void SetRecordingState(RecordingState state)
        {
            var prop = typeof(RecordingManager).GetProperty(
                "CurrentState",
                BindingFlags.Public | BindingFlags.Instance);
            prop.SetValue(_manager, state);
        }

        [Test]
        public void RecordingManager_InitialState_ShouldBeIdle()
        {
            Assert.AreEqual(RecordingState.Idle, _manager.CurrentState);
            Assert.IsNull(_manager.RecordId);
        }

        [Test]
        public void RecordingManager_SetRecordId_ShouldUpdateProperty()
        {
            string testId = "test-record-123";
            _manager.SetRecordId(testId);

            Assert.AreEqual(testId, _manager.RecordId);
        }

        [Test]
        public void RecordingManager_CanTransitionTo_ShouldValidateTransitions()
        {
            // Initial state is Idle
            Assert.AreEqual(RecordingState.Idle, _manager.CurrentState);

            // Can always reset to Idle
            Assert.IsTrue(_manager.CanTransitionTo(RecordingState.Idle));

            // From Idle, cannot skip to Aligning (must go through Scanning first)
            Assert.IsFalse(_manager.CanTransitionTo(RecordingState.Aligning));

            // From Idle, can transition to Scanning (one step forward: Idle=0, Scanning=1)
            Assert.IsTrue(_manager.CanTransitionTo(RecordingState.Scanning));
        }

        [Test]
        public void RecordingManager_CaptureBackground_InWrongState_ShouldThrow()
        {
            // Manager starts in Idle; CaptureBackground requires Scanning.
            Assert.AreEqual(RecordingState.Idle, _manager.CurrentState);

            Assert.Throws<InvalidOperationException>(() => _manager.CaptureBackground());
        }

        [Test]
        public void RecordingManager_StartObjectScan_InWrongState_ShouldThrow()
        {
            Assert.AreEqual(RecordingState.Idle, _manager.CurrentState);

            Assert.Throws<InvalidOperationException>(() => _manager.StartObjectScan());
        }

        [Test]
        public void RecordingManager_ConfirmScan_InWrongState_ShouldThrow()
        {
            Assert.AreEqual(RecordingState.Idle, _manager.CurrentState);

            Assert.Throws<InvalidOperationException>(() => _manager.ConfirmScan());
        }

        [Test]
        public void RecordingManager_Rescan_InWrongState_ShouldThrow()
        {
            Assert.AreEqual(RecordingState.Idle, _manager.CurrentState);

            Assert.Throws<InvalidOperationException>(() => _manager.Rescan());
        }

        [Test]
        public void RecordingManager_ScanPassthrough_WithNullScanManager_ShouldLogErrorNotThrow()
        {
            // Force Scanning state so the state gate passes, then verify that
            // a null _scanManager (no ScanManager in this test scene) causes
            // the passthrough to log an error and return cleanly instead of throwing.
            SetRecordingState(RecordingState.Scanning);

            // Confirm _scanManager is null (no ScanManager component exists in this scene)
            var field = typeof(RecordingManager).GetField(
                "_scanManager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(field.GetValue(_manager),
                "Test precondition: _scanManager should be null when no ScanManager is in the scene");

            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("ScanManager is null"));

            Assert.DoesNotThrow(() => _manager.CaptureBackground(),
                "Passthrough should log error and return when _scanManager is null, not throw");
        }
    }
}

#endif
