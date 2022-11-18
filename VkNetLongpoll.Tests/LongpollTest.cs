using Moq;
using VkNet.Abstractions;
using System.Threading.Tasks;
using VkNetLongpoll.Tests.Utils;
using System.Text.RegularExpressions;

namespace VkNetLongpoll.Tests;
public class LongpollTest
{
    private static TestDataLoader testDataLoader = new TestDataLoader();
    private VkNet.Utils.VkResponse lpInvokeTestResponse = new VkNet.Utils.VkResponse(testDataLoader.loadJSON("LongpollUpdateResponse"));
    private Mock<IVkApi> vkAPIMock;
    private Longpoll lp;
    [SetUp]
    public void Setup()
    {
        vkAPIMock = new Mock<IVkApi>();
        vkAPIMock
            .Setup(ld => ld.Groups.GetLongPollServerAsync(It.IsAny<ulong>()))
            .ReturnsAsync(new VkNet.Model.LongPollServerResponse()
            {
                Key = "test",
                Ts = "1",
                Pts = 1,
                Server = "https://test.com"
            });
        vkAPIMock
            .Setup(ld => ld.CallLongPollAsync(It.IsAny<string>(), It.IsAny<VkNet.Utils.VkParameters>()))
            .ReturnsAsync(lpInvokeTestResponse);
        vkAPIMock
            .Setup(ld => ld.IsAuthorized)
            .Returns(true);

        lp = new Longpoll(vkAPIMock.Object, 1);
    }

    [Test]
    public async Task SingleInvokeTest()
    {
        bool eventHandled = false;
        lp.Handler.On<VkNet.Model.GroupUpdate.MessageNew>(VkNet.Enums.SafetyEnums.GroupUpdateType.MessageNew, evt =>
        {
            lp.Stop();
            eventHandled = true;
            return Task.CompletedTask;
        });

        await lp.Start();

        Assert.IsTrue(eventHandled);
    }
    [Test]
    public async Task PollingLoopTest()
    {
        int invokesCount = 0;
        lp.Handler.On<VkNet.Model.GroupUpdate.MessageNew>(VkNet.Enums.SafetyEnums.GroupUpdateType.MessageNew, evt =>
        {
            if (invokesCount == 3)
                lp.Stop();
            invokesCount++;
            return Task.CompletedTask;
        });

        await lp.Start();

        Assert.AreEqual(invokesCount, 4);
    }
    [Test]
    public async Task AsyncHandlingTest()
    {
        long commandExecuteOffset = 0;
        lp.Handler.HearCommand(new Regex(@".*"), async (ctx, next) =>
        {
            commandExecuteOffset = System.DateTime.Now.Millisecond;
            await Task.Delay(300);
            next();
        });
        lp.Handler.HearCommand(new Regex(@".*"), _ =>
        {
            commandExecuteOffset = System.DateTime.Now.Millisecond- commandExecuteOffset;
            lp.Stop();
            return Task.CompletedTask;
        });

        await lp.Start();

        Assert.IsTrue(commandExecuteOffset <= 600);
    }

    [Test]
    public void PassingExceptionFromHandlerTest()
    {
        lp.Handler.HearCommand(new Regex(@".*"), ctx =>
        {
            lp.Stop();
            throw new System.Exception();
        });

        var lpTask = lp.Start();

        Assert.ThrowsAsync<System.Exception>(async () => await lpTask);
    }
    [Test]
    public async Task FailedResponseHandlingTest()
    {
        vkAPIMock
            .SetupSequence(ld => ld.CallLongPollAsync(It.IsAny<string>(), It.IsAny<VkNet.Utils.VkParameters>()))
            .ReturnsAsync(new VkNet.Utils.VkResponse(testDataLoader.loadJSON("FailedLongpollUpdateResponse")))
            .ReturnsAsync(lpInvokeTestResponse);

        await SingleInvokeTest();
    }
}