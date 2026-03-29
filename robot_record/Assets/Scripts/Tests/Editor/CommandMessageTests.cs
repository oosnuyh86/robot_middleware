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
    }
}

#endif
