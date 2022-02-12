using System;
using System.Threading.Tasks;

namespace VkNetLongpoll
{
    public class GroupCommandHandler : CommandHandler
    {
        private Func<VkNet.Model.Message, bool> predicat;
        public GroupCommandHandler(Func<VkNet.Model.Message, bool> predicat, MiddlewareChain<MessageContext> chain) : base(chain)
        {
            this.predicat = predicat;
        }
        public override void HearCommand(CommandMatchPattern handlerParams, Func<MessageContext, Action, Task> handler)
        {
            handlerParams.Predicate = msg => predicat(msg) && (handlerParams?.Predicate(msg) ?? true);
            commands.Use(new MesssageEventHandler(handlerParams, handler));
        }
    }
}
