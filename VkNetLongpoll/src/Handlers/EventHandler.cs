using System;
using System.Linq;
using System.Threading.Tasks;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;

namespace VkNetLongpoll
{
    public class LongpollGroupUpdateEventHandler<T> : EventHandler<T, GroupUpdate>
    {
        private readonly GroupUpdateType eventType;
        private readonly string eventFieldName;
        public LongpollGroupUpdateEventHandler(GroupUpdateType eventType, Func<T, Action, Task> handler) : base(handler)
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
    public class EventHandler<T, R> : IEventHandler<T, R>
    {
        protected readonly Func<T, Action, Task> handlerFn;
        public EventHandler(Func<T, Action, Task> handler)
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

        public static implicit operator Middleware<dynamic>(EventHandler<T, R> body) =>
            new Middleware<dynamic>((context, next) => body.Handle(context, next));
        public static implicit operator Middleware<T>(EventHandler<T, R> body) =>
            new Middleware<T>((context, next) => body.Handle(context, next));
        public static implicit operator Middleware<R>(EventHandler<T, R> body) =>
            new Middleware<R>((context, next) => body.Handle(context, next));
    }
    public interface IEventHandler<T, EvtSource>
    {
        public bool IsMatch(EvtSource evt);
        public bool IsMatch(T evt);
        public Task Handle(EvtSource evt, Action next);
        public Task Handle(T evt, Action next);
    }
}
