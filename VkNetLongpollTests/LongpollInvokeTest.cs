using Moq;
using VkNetLongpoll;
using NUnit.Framework;
using System.Threading.Tasks;
using VkNet.Abstractions;
using VkNetLongpollTests.Utils;

namespace VkNetLongpollTests
{
    public class LongpollInvokeTest
    {
        private TestDataLoader testDataLoader;
        private VkNet.Utils.VkResponse lpInvokeTestResponse;
        [SetUp]
        public void Setup()
        {
            testDataLoader = new TestDataLoader(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TestData"));
            lpInvokeTestResponse = new VkNet.Utils.VkResponse(testDataLoader.GetJSON("LongpollUpdateResponse"));
        }

        private Mock<IVkApi> vkAPIMock;
        [SetUp]
        public void VkAPISetup()
        {
            var mock = new Mock<IVkApi>();
            mock.Setup(ld => ld.Groups.GetLongPollServerAsync(It.IsAny<ulong>())).ReturnsAsync(new VkNet.Model.LongPollServerResponse()
            {
                Key = "test",
                Ts = "1",
                Pts = 1,
                Server = "https://test.com"
            });
            mock.Setup(ld => ld.CallLongPollAsync(It.IsAny<string>(), It.IsAny<VkNet.Utils.VkParameters>())).ReturnsAsync(lpInvokeTestResponse);
            mock.Setup(ld => ld.IsAuthorized).Returns(true);

            vkAPIMock = mock;
        }

        [Test]
        public void SingleInvokeTest()
        {
            bool eventHandled = false;
            var lp = new Longpoll(vkAPIMock.Object, 1);
            lp.Handler.On<VkNet.Model.GroupUpdate.MessageNew>(VkNet.Enums.SafetyEnums.GroupUpdateType.MessageNew, evt =>
            {
                lp.Stop();
                eventHandled = true;
                return Task.CompletedTask;
            });

            lp.Start();

            Assert.IsTrue(eventHandled);
        }
        [Test]
        public void PollingLoopTest()
        {
            int invokesCount = 0;
            var lp = new Longpoll(vkAPIMock.Object, 1);
            lp.Handler.On<VkNet.Model.GroupUpdate.MessageNew>(VkNet.Enums.SafetyEnums.GroupUpdateType.MessageNew, evt =>
            {
                if (invokesCount == 3)
                    lp.Stop();
                invokesCount++;
                return Task.CompletedTask;
            });

            lp.Start();

            Assert.AreEqual(invokesCount, 4);
        }
        [Test]
        public void FailedResponseHandlingTest()
        {
            vkAPIMock.SetupSequence(ld => ld.CallLongPollAsync(It.IsAny<string>(), It.IsAny<VkNet.Utils.VkParameters>()))
                .ReturnsAsync(new VkNet.Utils.VkResponse(testDataLoader.GetJSON("FailedLongpollUpdateResponse")))
                .ReturnsAsync(lpInvokeTestResponse);

            SingleInvokeTest();
        }
    }
}
