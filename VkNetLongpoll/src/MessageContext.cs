using System;
using System.Linq;
using VkNet.Model;
using System.Net.Http;
using VkNet.Abstractions;
using System.Threading.Tasks;
using VkNet.Enums.SafetyEnums;
using VkNet.Model.GroupUpdate;
using VkNet.Model.RequestParams;
using System.Text.RegularExpressions;

namespace VkNetLongpoll
{
    public class MessageContext
    {
        public readonly Message Body;
        public readonly ClientInfo ClientInfo;
        public IVkApi Api;
        public Match Match;
        public HttpClient Client = new HttpClient();

        public MessageContext(GroupUpdate rawEvent) : this(rawEvent.MessageNew) { }
        public MessageContext(MessageNew rawMessage)
        {
            Body = rawMessage.Message;
            ClientInfo = rawMessage.ClientInfo;
        }

        public bool IsChat { get => Body.PeerId > 2e9; }

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
                PeerId = Body.PeerId
            };
            if (IsChat)
                @params.Forward.ConversationMessageIds = new[] { Body.ConversationMessageId.Value };
            else
                @params.Forward.MessageIds = new[] { Body.Id.Value };


            return Send(@params);
        }
        public Task<long> ReplyAsync(string text) => ReplyAsync(new MessagesSendParams { Message = text });
        public Task<long> ReplyAsync(MessagesSendParams @params)
        {
            @params.Forward = new MessageForward
            {
                IsReply = true,
                PeerId = Body.PeerId
            };
            if (IsChat)
                @params.Forward.ConversationMessageIds = new[] { Body.ConversationMessageId.Value };
            else
                @params.Forward.MessageIds = new[] { Body.Id.Value };

            return SendAsync(@params);
        }

        public Task<long> SendPhoto(byte[] photo, string text) => SendPhoto(photo, new MessagesSendParams { Message = text });
        public async Task<long> SendPhoto(byte[] photo, MessagesSendParams @params = null)
        {
            var uploadServer = await Api.Photo.GetMessagesUploadServerAsync(Body.PeerId.Value);

            var form = new MultipartFormDataContent();
            form.Headers.ContentType.MediaType = "multipart/form-data";
            form.Add(new ByteArrayContent(photo, 0, photo.Length), "photo", "file.jpg");

            var photoResponse = await (await Client.PostAsync(uploadServer.UploadUrl, form)).Content.ReadAsStringAsync();
            (@params ??= new MessagesSendParams()).Attachments = await Api.Photo.SaveMessagesPhotoAsync(photoResponse);
            return await SendAsync(@params);
        }

        public Task<long> SendDoc(DocumentSource doc, string text, DocMessageType docType = null) =>
            SendDoc(doc, new MessagesSendParams { Message = text }, docType);
        public async Task<long> SendDoc(DocumentSource doc, MessagesSendParams @params = null, DocMessageType docType = null)
        {
            var uploadServer = await Api.Docs.GetMessagesUploadServerAsync(Body.PeerId.Value, docType ?? DocMessageType.Doc);

            var form = new MultipartFormDataContent();
            form.Headers.ContentType.MediaType = "multipart/form-data";
            form.Add(new ByteArrayContent(doc.Body, 0, doc.Body.Length), doc.Name, $"{doc.Name}.{doc.Type.Replace('.', '\0')}");

            var response = await (await Client.PostAsync(uploadServer.UploadUrl, form)).Content.ReadAsStringAsync();
            (@params ??= new MessagesSendParams()).Attachments = (await Api.Docs.SaveAsync(response, doc.Name, "")).Select(a => a.Instance);
            return await SendAsync(@params);
        }

        /// <param name="audio">Audio body. Should be encoded to mp3 by default.</param>
        public Task<long> SendAudioMessage(byte[] audio, MessagesSendParams @params = null) =>
            SendAudioMessage(audio, "mp3", @params);

        /// <param name="audio">VKAPI supports only mp3 and ogg as type for an audio message.</param>
        public Task<long> SendAudioMessage(byte[] audio, string type, MessagesSendParams @params = null) =>
            type != "mp3" && type != "ogg"
                ? throw new ArgumentException("Invalid audio type. VKAPI supports only mp3 and ogg as type for an audio message.")
                : SendDoc(new DocumentSource { Type = type, Body = audio }, @params, DocMessageType.AudioMessage);

        public bool Edit(string text) => Edit(new MessageEditParams { Message = text });
        public bool Edit(MessageEditParams @params)
        {
            @params.PeerId = Body.PeerId.Value;
            if (Body.PeerId > 2e9)
                @params.ConversationMessageId = Body.ConversationMessageId.Value;
            else
                @params.MessageId = Body.Id.Value;

            return Api.Messages.Edit(@params);
        }
        public Task<bool> EditAsync(string text) => EditAsync(new MessageEditParams { Message = text });
        public Task<bool> EditAsync(MessageEditParams @params)
        {
            @params.PeerId = Body.PeerId.Value;
            if (Body.PeerId > 2e9)
                @params.ConversationMessageId = Body.ConversationMessageId.Value;
            else
                @params.MessageId = Body.Id.Value;

            return Api.Messages.EditAsync(@params);
        }

        public bool Delete(bool deleteFoAll = true, bool isSpam = false) => (IsChat
            ? Api.Messages.Delete(new[] { (ulong)Body.ConversationMessageId }, (ulong)Body.PeerId, isSpam, null, deleteFoAll)
            : Api.Messages.Delete(new[] { (ulong)Body.Id }, isSpam, null, deleteFoAll))?.FirstOrDefault().Value ?? false;
        public async Task<bool> DeleteAsync(bool deleteFoAll = true, bool isSpam = false) => (IsChat
            ? await Api.Messages.DeleteAsync(new[] { (ulong)Body.ConversationMessageId }, (ulong)Body.PeerId, isSpam, null, deleteFoAll)
            : await Api.Messages.DeleteAsync(new[] { (ulong)Body.Id }, isSpam, null, deleteFoAll))?.FirstOrDefault().Value ?? false;

        public static explicit operator MessageContext(GroupUpdate rawEvent) => new MessageContext(rawEvent);
        public static explicit operator MessageContext(MessageNew rawMessage) => new MessageContext(rawMessage);
    }

    public class DocumentSource
    {
        public string Type;
        public byte[] Body;
        public string Name = "file";
    }
}
