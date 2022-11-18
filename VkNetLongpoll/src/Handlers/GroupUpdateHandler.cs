using System;
using System.Linq;
using VkNetLongpoll.Utils;
using System.Threading.Tasks;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;

namespace VkNetLongpoll.Handlers;
public class GroupUpdateHandler<T> : EventHandler<T, GroupUpdate>
{
    private readonly GroupUpdateType eventType;
    private readonly string eventFieldName;
    public GroupUpdateHandler(GroupUpdateType eventType, Func<T, Action, Task> handler) : base(handler)
    {
        this.eventType = eventType;
        eventFieldName = typeof(GroupUpdateType)
            .GetFields()
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