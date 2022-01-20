using System;
using VkNetLongpoll;
using NUnit.Framework;
using System.Threading.Tasks;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;
using VkNetLongpollTests.Utils;
using System.Text.RegularExpressions;

namespace VkNetLongpollTests
{
    public class LongpollEventHandlerTest
    {
        private GroupUpdate lpMessageNewEvent;
        private Func<MessageContext, Action, Task> testCommandHandler = (_, next) =>
        {
            next();
            return Task.CompletedTask;
        };
        private TestDataLoader testDataLoader;
        [SetUp]
        public void Setup()
        {
            testDataLoader = new TestDataLoader(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "testData"));
            lpMessageNewEvent = GroupUpdate.FromJson(new VkNet.Utils.VkResponse(testDataLoader.GetJSON("NewMessageUpdate")));
        }

        [Test]
        public void AddCommandTest()
        {
            var lpHandler = new LongpollEventHandler();

            lpHandler.HearCommand("/test", testCommandHandler);
            lpHandler.HearCommand(new Regex(@"^\/test"), testCommandHandler);
            lpHandler.HearCommand(new[] { "/test", "/test2" }, testCommandHandler);
            lpHandler.HearCommand(new EventMessageMatchPattern { Text = "/test", Texts = new[] { "/test2" }, Regex = new Regex(@"^\/test") }, testCommandHandler);

            Assert.AreEqual(4, lpHandler.CommandsCount);
        }

        [Test]
        public async Task CommandHandlingTest()
        {
            int commandsHandled = 0;
            var lpHandler = new LongpollEventHandler();
            Func<MessageContext, Action, Task> cmdHandler = (ctx, next) =>
            {
                next();
                commandsHandled++;
                return Task.CompletedTask;
            };
            lpHandler.HearCommand(new EventMessageMatchPattern { 
                Text = "/test1", 
                Texts = new[] { "/test2", "test/3" },
                Regex = new Regex(@"^\/test foo$", RegexOptions.IgnoreCase)
            }, cmdHandler);
            lpHandler.HearCommand(new EventMessageMatchPattern
            {
                Regex = new Regex(@"^\/msg (.*)"),
                Predicate = ctx => ctx.Text.Length <= 10
            }, cmdHandler);
            lpHandler.HearCommand(new EventMessageMatchPattern
            {
                Text = "/copy img",
                Attachments = new[] { typeof(VkNet.Model.Attachments.Photo) }
            }, cmdHandler);

            var commands = new[] { "/test1", "/test2", "test/3", "/test foo", "/tesT foo", "", "not a cmd", "not a cmd2", "/msg succ", "/msg failed" };
            foreach (var command in commands)
            {
                lpMessageNewEvent.MessageNew.Message.Text = command;
                await lpHandler.Handle(lpMessageNewEvent);
            }
            lpMessageNewEvent.MessageNew.Message.Text = "/copy img";
            await lpHandler.Handle(lpMessageNewEvent);
            lpMessageNewEvent.MessageNew.Message.Attachments = new System.Collections.ObjectModel.ReadOnlyCollection<VkNet.Model.Attachments.Attachment>(new[] { new VkNet.Model.Attachments.Attachment { Type = typeof(VkNet.Model.Attachments.Photo) } });
            await lpHandler.Handle(lpMessageNewEvent);

            Assert.AreEqual(7, commandsHandled);
        }

        [Test]
        public async Task CommandHandlingMatchTest()
        {
            int regexGroupsCount = 0;
            var lpHandler = new LongpollEventHandler();
            lpHandler.HearCommand(new EventMessageMatchPattern
            {
                Regex = new Regex(@"^\/test (foo) (\d) (s|f)", RegexOptions.IgnoreCase)
            }, (ctx, next) =>
            {
                regexGroupsCount = ctx.Match.Groups.Count;
                return Task.CompletedTask;
            });

            lpMessageNewEvent.MessageNew.Message.Text = "/test foo 3 s";
            await lpHandler.Handle(lpMessageNewEvent);
            Assert.AreEqual(4, regexGroupsCount);
        }

        [Test]
        public async Task EventHandlingTest()
        {
            int eventsHandled = 0;
            var lpHandler = new LongpollEventHandler();
            Func<dynamic, Action, Task> testHandler = (_, next) => 
            {
                next();
                eventsHandled++;
                return Task.CompletedTask;
            };

            lpHandler.On<MessageNew>(GroupUpdateType.MessageNew, testHandler);
            lpHandler.On<MessageContext>(GroupUpdateType.MessageNew, testHandler);
            lpHandler.On<GroupLeave>(GroupUpdateType.GroupLeave, testHandler);

            await lpHandler.Handle(lpMessageNewEvent);

            Assert.AreEqual(2, eventsHandled);
        }
    }
}