using Moq;
using System;
using VkNetLongpoll;
using NUnit.Framework;
using VkNet.Abstractions;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;
using VkNet.Model.RequestParams;
using VkNetLongpollTests.Utils;

namespace VkNetLongpollTests
{
    public class MessageContextMethodsTest
    {
        private GroupUpdate lpMessageNewEvent;
        private TestDataLoader testDataLoader;
        private Mock<IVkApi> vkAPIMock;
        [SetUp]
        public void Setup()
        {
            testDataLoader = new TestDataLoader(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "testData"));
            lpMessageNewEvent = GroupUpdate.FromJson(new VkNet.Utils.VkResponse(testDataLoader.GetJSON("NewMessageUpdate")));
            vkAPIMock = new Mock<IVkApi>();
            vkAPIMock.SetupSequence(ld => ld.Messages.Send(It.IsAny<MessagesSendParams>()))
                .Returns(1)
                .Returns(2);
        }

        [Test]
        public void SendTest()
        {
            long[] results = new long[2];
            MessageContext ctx = new MessageContext(lpMessageNewEvent);
            ctx.Api = vkAPIMock.Object;

            results[0] = ctx.Send(new MessagesSendParams { Message = "hello" });
            results[1] = ctx.Send("there");

            Assert.AreEqual(1, results[0]);
            Assert.AreEqual(2, results[1]);
        }
        [Test]
        public void ReplyTest()
        {
            long[] results = new long[2];
            MessageContext ctx = new MessageContext(lpMessageNewEvent);
            ctx.Api = vkAPIMock.Object;

            results[0] = ctx.Reply(new MessagesSendParams { Message = "hello" });
            results[1] = ctx.Reply("there");

            Assert.AreEqual(1, results[0]);
            Assert.AreEqual(2, results[1]);
        }
    }
}
