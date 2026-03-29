#if UNITY_EDITOR

using NUnit.Framework;
using RobotMiddleware.Models;

namespace RobotMiddleware.Tests.Editor
{
    [TestFixture]
    public class RecordingStateTests
    {
        [Test]
        public void RecordingState_Enum_ShouldHaveCorrectValues()
        {
            Assert.AreEqual(0, (int)RecordingState.Idle);
            Assert.AreEqual(1, (int)RecordingState.Scanning);
            Assert.AreEqual(2, (int)RecordingState.Aligning);
            Assert.AreEqual(3, (int)RecordingState.Recording);
            Assert.AreEqual(4, (int)RecordingState.Uploading);
            Assert.AreEqual(5, (int)RecordingState.Training);
            Assert.AreEqual(6, (int)RecordingState.Validating);
            Assert.AreEqual(7, (int)RecordingState.Approved);
            Assert.AreEqual(8, (int)RecordingState.Executing);
            Assert.AreEqual(9, (int)RecordingState.Complete);
            Assert.AreEqual(10, (int)RecordingState.Failed);
        }

        [Test]
        public void RecordingState_Enum_ShouldBeSequential()
        {
            var states = new[]
            {
                RecordingState.Idle,
                RecordingState.Scanning,
                RecordingState.Aligning,
                RecordingState.Recording,
                RecordingState.Uploading,
                RecordingState.Training,
                RecordingState.Validating,
                RecordingState.Approved,
                RecordingState.Executing,
                RecordingState.Complete,
                RecordingState.Failed
            };

            for (int i = 0; i < states.Length; i++)
            {
                Assert.AreEqual(i, (int)states[i]);
            }
        }
    }
}

#endif
