using System;
using System.Threading.Tasks;

namespace VkNetLongpoll
{
    public class GroupCommandHandler : CommandHandler
    {
        private Predicate<VkNet.Model.Message> predicate;
        public GroupCommandHandler(Predicate<VkNet.Model.Message> predicate, MiddlewareChain<MessageContext> chain) : base(chain)
        {
            this.predicate = predicate;
        }
        public override void HearCommand(CommandMatchPattern handlerParams, Func<MessageContext, Action, Task> handler)
        {
            var previousPredicate = handlerParams.Predicate;
            handlerParams.Predicate = msg => predicate(msg) && (previousPredicate?.Invoke(msg) ?? true);
            commands.Use(new MesssageEventHandler(handlerParams, handler));
        }
    }
}
