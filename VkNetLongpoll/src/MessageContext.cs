using System;
using VkNet.Model;
using System.Net.Http;
using VkNet.Abstractions;
using System.Threading.Tasks;
using VkNet.Model.GroupUpdate;
using VkNet.Model.RequestParams;

namespace VkNetLongpoll
{
    public class MessageContext
    {
        public readonly Message Body;
        public readonly ClientInfo ClientInfo;
        public IVkApi Api;

        public MessageContext(GroupUpdate rawEvent) : this(rawEvent.MessageNew) { }
        public MessageContext(MessageNew rawMessage)
        {
            Body = rawMessage.Message;
            ClientInfo = rawMessage.ClientInfo;
        }

        public long Send(string text) => Send(new MessagesSendParams { Message = text });
        public long Send(MessagesSendParams @params)
        {
            @params.PeerId = Body.PeerId;
            @params.RandomId = new Random().Next();
            return Api.Messages.Send(@params);
        }
        public Task<long> SendAsync(string text) => SendAsync(new MessagesSendParams { Message = text });
        public Task<long> SendAsync(MessagesSendParams @params)
        {
            @params.PeerId = Body.PeerId;
            @params.RandomId = new Random().Next();
            return Api.Messages.SendAsync(@params);
        }

        public long Reply(string text) => Reply(new MessagesSendParams { Message = text });
        public long Reply(MessagesSendParams @params)
        {
            @params.Forward = new MessageForward
            {
                IsReply = true,
                PeerId = Body.PeerId,
                ConversationMessageIds = new long[] { (Body.PeerId > 2e9 ? Body.ConversationMessageId : Body.Id).Value }
            };
            return Send(@params);
        }
        public Task<long> ReplyAsync(string text) => ReplyAsync(new MessagesSendParams { Message = text });
        public Task<long> ReplyAsync(MessagesSendParams @params)
        {
            @params.Forward = new MessageForward
            {
                IsReply = true,
                PeerId = Body.PeerId,
                ConversationMessageIds = new long[] { (Body.PeerId > 2e9 ? Body.ConversationMessageId : Body.Id).Value }
            };
            return SendAsync(@params);
        }

        public Task<long> SendPhoto(byte[] photo, string text) => SendPhoto(photo, new MessagesSendParams { Message = text });
        public async Task<long> SendPhoto(byte[] photo, MessagesSendParams @params = null)
        {
            var uploadServer = Api.Photo.GetMessagesUploadServer(Body.PeerId.Value);
            var client = new HttpClient();

            var form = new MultipartFormDataContent();
            form.Headers.ContentType.MediaType = "multipart/form-data";
            form.Add(new ByteArrayContent(photo, 0, photo.Length), "photo", "file.jpg");

            var photoResponse = await (await client.PostAsync(uploadServer.UploadUrl, form)).Content.ReadAsStringAsync();
            (@params ??= new MessagesSendParams()).Attachments = Api.Photo.SaveMessagesPhoto(photoResponse);
            return await SendAsync(@params);
        }

        public static explicit operator MessageContext(GroupUpdate rawEvent) => new MessageContext(rawEvent);
        public static explicit operator MessageContext(MessageNew rawMessage) => new MessageContext(rawMessage);
    }
}
