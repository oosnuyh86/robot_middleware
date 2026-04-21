#if UNITY_EDITOR

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RobotMiddleware.DataChannel;

namespace RobotMiddleware.Tests.Editor
{
    [TestFixture]
    public class CommandMessageTests
    {
        [Test]
        public void CommandMessage_Creation_ShouldInitializeWithDefaults()
        {
            var msg = new CommandMessage();

            Assert.IsNotEmpty(msg.id);
            Assert.AreEqual("COMMAND", msg.type);
            Assert.IsNotEmpty(msg.timestamp);
            Assert.IsNotEmpty(msg.clientId);
        }

        [Test]
        public void CommandMessage_WithAction_ShouldSetActionCorrectly()
        {
            var msg = new CommandMessage(CommandAction.ALIGN_SENSORS, "metadata");

            Assert.AreEqual("ALIGN_SENSORS", msg.action);
            Assert.AreEqual("metadata", msg.payload);
            Assert.AreEqual("COMMAND", msg.type);
        }

        [Test]
        public void CommandMessage_ToJson_ShouldSerializeCorrectly()
        {
            var msg = new CommandMessage(CommandAction.START_SCAN);
            var json = msg.ToJson();

            Assert.IsNotEmpty(json);
            StringAssert.Contains("START_SCAN", json);
            StringAssert.Contains("COMMAND", json);
        }

        [Test]
        public void CommandMessage_FromJson_ShouldDeserializeCorrectly()
        {
            var original = new CommandMessage(CommandAction.ALIGN_SENSORS, "test_payload");
            var json = original.ToJson();

            var deserialized = CommandMessage.FromJson(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual("ALIGN_SENSORS", deserialized.action);
            Assert.AreEqual("test_payload", deserialized.payload);
            Assert.AreEqual("COMMAND", deserialized.type);
        }

        [Test]
        public void CommandMessage_FromJson_WithMalformedJson_ShouldReturnNull()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Failed to parse JSON"));
            var result = CommandMessage.FromJson("{ invalid json }");

            Assert.IsNull(result);
        }

        [TestCase(CommandAction.CAPTURE_BACKGROUND, "CAPTURE_BACKGROUND")]
        [TestCase(CommandAction.START_OBJECT_SCAN, "START_OBJECT_SCAN")]
        [TestCase(CommandAction.CONFIRM_SCAN, "CONFIRM_SCAN")]
        [TestCase(CommandAction.RESCAN, "RESCAN")]
        public void CommandMessage_ScanSubCommands_ShouldRoundTrip(
            CommandAction action, string expectedActionString)
        {
            var original = new CommandMessage(action, "rec-xyz-123");
            var json = original.ToJson();

            StringAssert.Contains(expectedActionString, json);

            var deserialized = CommandMessage.FromJson(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(expectedActionString, deserialized.action);
            Assert.AreEqual("rec-xyz-123", deserialized.payload);
            Assert.AreEqual("COMMAND", deserialized.type);
        }
    }
}

#endif
