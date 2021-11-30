using System;
using System.Collections.Generic;
using System.Text;
using VkNet.Model;
using VkNet.Model.GroupUpdate;

namespace VkNetLongpoll
{
    public class MessageContext
    {
        public readonly Message Body;
        public readonly ClientInfo ClientInfo;

        public MessageContext(GroupUpdate rawEvent) : this(rawEvent.MessageNew) { }
        public MessageContext(MessageNew rawMessage)
        {
            Body = rawMessage.Message;
            ClientInfo = rawMessage.ClientInfo;
        }

        public static explicit operator MessageContext(GroupUpdate rawEvent) => new MessageContext(rawEvent);
        public static explicit operator MessageContext(MessageNew rawMessage) => new MessageContext(rawMessage);
    }
}
