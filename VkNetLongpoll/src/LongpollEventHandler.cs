using System;
using System.Linq;
using VkNet.Abstractions;
using System.Threading.Tasks;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;
using System.Text.RegularExpressions;

namespace VkNetLongpoll
{
    public class LongpollEventHandler
    {
        private MiddlewareChain<dynamic> events = new MiddlewareChain<dynamic>();
        private MiddlewareChain<MessageContext> commands = new MiddlewareChain<MessageContext>();

        public async Task Handle(GroupUpdate longpollEvent, IVkApi api = null)
        {
            await events.ExecuteAsync(longpollEvent);
                
            if (longpollEvent.MessageNew == null) return;
            var message = new MessageContext(longpollEvent);
            message.Api = api;
            await commands.ExecuteAsync(message);
        }

        public void On<T>(GroupUpdateType eventType, Func<T, Action, Task> handler) =>
            events.Use(new EventHandler<T>(eventType, handler));
        public void On<T>(GroupUpdateType eventType, Func<T, Task> handler) =>
            On<T>(eventType, (context, next) => handler(context));

        public void HearCommand(EventMessageHandlerParams handlerParams, Func<MessageContext, Action, Task> handler) =>
            commands.Use(new EventMessageHandler(handlerParams, handler));
        public void HearCommand(Regex regex, Func<MessageContext, Action, Task> handler) =>
            HearCommand(new EventMessageHandlerParams() { regex = regex }, handler);
        public void HearCommand(string textMatch, Func<MessageContext, Action, Task> handler) =>
            HearCommand(new EventMessageHandlerParams() { text = textMatch }, handler);
        public void HearCommand(string[] textMatches, Func<MessageContext, Action, Task> handler) =>
            HearCommand(new EventMessageHandlerParams() { texts = textMatches }, handler);

        public void HearCommand(EventMessageHandlerParams handlerParams, Func<MessageContext, Task> handler) =>
            HearCommand(handlerParams, (context, next) => handler(context));
        public void HearCommand(Regex regex, Func<MessageContext, Task> handler) =>
            HearCommand(new EventMessageHandlerParams() { regex = regex }, handler);
        public void HearCommand(string textMatch, Func<MessageContext, Task> handler) =>
            HearCommand(new EventMessageHandlerParams() { text = textMatch }, handler);
        public void HearCommand(string[] textMatches, Func<MessageContext, Task> handler) =>
            HearCommand(new EventMessageHandlerParams() { texts = textMatches }, handler);

        public int CommandsCount { get => commands.Count; }
        public int EventsCount { get => events.Count; }
    }

    class BaseEventHandler<T, R> : IEventHandler<R>
    {
        protected readonly Func<T, Action, Task> handlerFn;
        public BaseEventHandler(Func<T, Action, Task> handler)
        {
            handlerFn = handler;
        }
        public virtual bool IsMatch(R evt) => true;
        public virtual bool IsMatch(T evt) => true;
        public virtual Task Handle(R evt, Action next = null) => Handle(Convert.ChangeType((dynamic)evt, typeof(T)), next);
        public virtual Task Handle(T evt, Action next = null)
        {
            if (IsMatch(evt))
                return handlerFn(evt, next ?? (() => { }));
            next?.Invoke();
            return Task.CompletedTask;
        }

        public static implicit operator Middleware<dynamic>(BaseEventHandler<T, R> body) =>
            new Middleware<dynamic>((context, next) => body.Handle(context, next));
        public static implicit operator Middleware<T>(BaseEventHandler<T, R> body) =>
            new Middleware<T>((context, next) => body.Handle(context, next));
        public static implicit operator Middleware<R>(BaseEventHandler<T, R> body) =>
            new Middleware<R>((context, next) => body.Handle(context, next));
    }

    class EventHandler<T> : BaseEventHandler<T, GroupUpdate>
    {
        private readonly GroupUpdateType eventType;
        private readonly string eventFieldName;
        public EventHandler(GroupUpdateType eventType, Func<T, Action, Task> handler) : base(handler)
        {
            this.eventType = eventType;
            eventFieldName = typeof(GroupUpdateType).GetFields()
                .Where(prop => prop.GetValue(null).ToString() == eventType.ToString())
                .Select(prop => prop?.Name)
                .FirstOrDefault();
        }
        public override bool IsMatch(GroupUpdate evt) => evt.Type == eventType;
        public override Task Handle(GroupUpdate evt, Action next = null)
        {
            var evtBody = typeof(GroupUpdate).GetProperty(eventFieldName).GetValue(evt);
            if (IsMatch(evt))
                return handlerFn((T)(dynamic)Convert.ChangeType(evtBody, evtBody.GetType()), next);
            next?.Invoke();
            return Task.CompletedTask;
        }
    }
    class EventMessageHandler : BaseEventHandler<MessageContext, MessageNew>
    {
        private readonly EventMessageHandlerParams handlerParams;
        public EventMessageHandler(EventMessageHandlerParams handlerParams, Func<MessageContext, Action, Task> handler) : base(handler)
        {
            this.handlerParams = handlerParams;
        }
        public override bool IsMatch(MessageContext message)
        {
            var body = message.Body;
            if (!(body?.Text?.Any() ?? false)) 
                return false;
            var textMatches = (handlerParams?.texts ?? new[] { String.Empty }).Append(handlerParams?.text);

            return (handlerParams?.regex?.IsMatch(body.Text) ?? true) || (textMatches?.Count() > 0 && textMatches.Contains(body.Text));
        }
        public override Task Handle(MessageContext evt, Action next = null)
        {
            if (handlerParams?.regex?.IsMatch(evt.Body.Text) ?? false)
                evt.Match = handlerParams.regex.Match(evt.Body.Text);

            return base.Handle(evt, next);
        }
    }
    public class EventMessageHandlerParams
    {
        public Regex regex;
        public string text;
        public string[] texts;
    }
    interface IEventHandler<T>
    {
        public bool IsMatch(T rawEvent);
        public Task Handle(T rawEvent, Action next);
    }
}
