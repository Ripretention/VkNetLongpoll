using System;
using System.Linq;
using VkNet.Abstractions;
using VkNetLongpoll.Utils;
using VkNetLongpoll.Contexts;
using System.Threading.Tasks;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;
using System.Text.RegularExpressions;

namespace VkNetLongpoll.Handlers;
public class LongpollEventHandler : CommandHandler
{
    private MiddlewareChain<dynamic> events = new MiddlewareChain<dynamic>();
    public LongpollEventHandler() : base() { }

    public async Task Handle(GroupUpdate longpollEvent, IVkApi api = null)
    {
        await events.ExecuteAsync(longpollEvent);
            
        if (longpollEvent.MessageNew == null) return;
        var message = new MessageContext(longpollEvent);
        message.Api = api;
        await commands.ExecuteAsync(message);
    }
    public GroupCommandHandler CreateGroup(Predicate<VkNet.Model.Message> predicate) =>
        new GroupCommandHandler(predicate, commands);

    public void On<T>(GroupUpdateType eventType, Func<T, Action, Task> handler) =>
        events.Use(new GroupUpdateHandler<T>(eventType, handler));
    public void On<T>(GroupUpdateType eventType, Func<T, Task> handler) =>
        On<T>(eventType, (context, next) => handler(context));

    public int EventsCount { get => events.Count; }
}