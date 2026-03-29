#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;
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
    }
}

#endif
