using Moq;
using Moq.Protected;
using VkNetLongpoll;
using NUnit.Framework;
using System.Net.Http;
using VkNet.Abstractions;
using System.Threading.Tasks;
using VkNet.Model.GroupUpdate;
using VkNet.Enums.SafetyEnums;
using VkNetLongpollTests.Utils;
using VkNet.Model.RequestParams;
using System.Collections.Generic;

namespace VkNetLongpollTests
{
    public class MessageContextMethodsTest
    {
        private static TestDataLoader testDataLoader = new TestDataLoader(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TestData"));
        private GroupUpdate lpMessageNewEvent = GroupUpdate.FromJson(new VkNet.Utils.VkResponse(testDataLoader.GetJSON("NewMessageUpdate")));
        private Mock<IVkApi> vkAPIMock;
        [SetUp]
        public void Setup()
        {
            vkAPIMock = new Mock<IVkApi>();
            vkAPIMock
                .SetupSequence(ld => ld.Messages.Send(It.IsAny<MessagesSendParams>()))
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

        [Test]
        public async Task SendDoc()
        {
            var httpClientHandlerMock = new Mock<HttpClientHandler>();
            httpClientHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage 
                { 
                    StatusCode = System.Net.HttpStatusCode.OK, 
                    Content = new StringContent("somedata") 
                });
            vkAPIMock
                .Setup(ld => ld.Docs.GetMessagesUploadServerAsync(It.IsAny<long>(), It.IsAny<DocMessageType>()))
                .ReturnsAsync(new VkNet.Model.UploadServerInfo
                {
                    UploadUrl = "https://vkapitest/fefwefweDFscsdsF",
                    AlbumId = 0,
                    UserId = 0
                });
            vkAPIMock
                .Setup(ld => ld.Docs.SaveAsync(It.Is<string>(v => v == "somedata"), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<VkNet.Model.Attachments.Attachment>() 
                { 
                    new VkNet.Model.Attachments.Attachment
                    {
                        Type = typeof(VkNet.Model.Attachments.AudioMessage)
                    }
                }.AsReadOnly());
            vkAPIMock
                .Setup(ld => ld.Messages.SendAsync(It.IsAny<MessagesSendParams>()))
                .ReturnsAsync(43);
            MessageContext ctx = new MessageContext(lpMessageNewEvent);
            ctx.Api = vkAPIMock.Object;
            ctx.Client = new HttpClient(httpClientHandlerMock.Object);

            var response = await ctx.SendDoc(new DocumentSource { Body = new byte[0], Name = "file", Type = "mp3" }, "doc message");

            Assert.AreEqual(43, response);
        }
    }
}
