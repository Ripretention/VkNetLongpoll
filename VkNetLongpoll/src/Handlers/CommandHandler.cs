using System;
using System.Linq;
using VkNetLongpoll.Utils;
using VkNetLongpoll.Contexts;
using System.Threading.Tasks;
using VkNet.Model.GroupUpdate;
using System.Text.RegularExpressions;

namespace VkNetLongpoll.Handlers;
public class CommandHandler : ICommandHandler
{
    protected MiddlewareChain<MessageContext> commands;
    public CommandHandler(MiddlewareChain<MessageContext> chain = null)
    {
        commands = chain ?? new MiddlewareChain<MessageContext>();
    }

    public virtual void HearCommand(CommandMatchPattern handlerParams, Func<MessageContext, Action, Task> handler) =>
        commands.Use(new MesssageEventHandler(handlerParams, handler));
    public void HearCommand(Regex regex, Func<MessageContext, Action, Task> handler) =>
        HearCommand(new CommandMatchPattern() { Regex = regex }, handler);
    public void HearCommand(string textMatch, Func<MessageContext, Action, Task> handler) =>
        HearCommand(new CommandMatchPattern() { Text = textMatch }, handler);
    public void HearCommand(string[] textMatches, Func<MessageContext, Action, Task> handler) =>
        HearCommand(new CommandMatchPattern() { Texts = textMatches }, handler);
    public void HearCommand(CommandMatchPattern handlerParams, Func<MessageContext, Task> handler) =>
        HearCommand(handlerParams, (context, next) => handler(context));
    public void HearCommand(Regex regex, Func<MessageContext, Task> handler) =>
        HearCommand(new CommandMatchPattern() { Regex = regex }, handler);
    public void HearCommand(string textMatch, Func<MessageContext, Task> handler) =>
        HearCommand(new CommandMatchPattern() { Text = textMatch }, handler);
    public void HearCommand(string[] textMatches, Func<MessageContext, Task> handler) =>
        HearCommand(new CommandMatchPattern() { Texts = textMatches }, handler);

    public int CommandsCount { get => commands.Count; }
}
public interface ICommandHandler
{
    public void HearCommand(CommandMatchPattern handlerParams, Func<MessageContext, Action, Task> handler);
}

public class MesssageEventHandler : EventHandler<MessageContext, MessageNew>
{
    private readonly CommandMatchPattern matchPattern;
    public MesssageEventHandler(CommandMatchPattern matchPattern, Func<MessageContext, Action, Task> handler) : base(handler)
    {
        this.matchPattern = matchPattern;
    }
    public override bool IsMatch(MessageContext message)
    {
        var body = message.Body;
        bool isEmptyMessage = (body?.Text?.Count() ?? 0) == 0;
        bool hasAttachments = (body.Attachments?.Count() ?? 0) > 0;
        bool hasMatchedAttachments = (matchPattern?.Attachments?.Count() ?? 0) != 0
            ? hasAttachments && (matchPattern?.Attachments?.All(aType => body.Attachments.Any(attach => attach.Type == aType)) ?? true)
            : true;
        if (!hasMatchedAttachments || !(matchPattern?.Predicate?.Invoke(body) ?? true) || isEmptyMessage)
            return false;

        var textMatches = (matchPattern?.Texts ?? Array.Empty<string>()).Append(matchPattern?.Text ?? "");
        return (matchPattern?.Regex?.IsMatch(body.Text) ?? false) || textMatches.Where(m => m != String.Empty).Contains(body.Text);
    }
    public override Task Handle(MessageContext evt, Action next = null)
    {
        if (matchPattern?.Regex?.IsMatch(evt.Body.Text) ?? false)
            evt.Match = matchPattern.Regex.Match(evt.Body.Text);

        return base.Handle(evt, next);
    }
}
public class CommandMatchPattern
{
    public Regex Regex { get; set; }
    public string Text { get; set; }
    public string[] Texts { get; set; }
    public Type[] Attachments { get; set; }
    public Predicate<VkNet.Model.Message> Predicate { get; set; }
}
