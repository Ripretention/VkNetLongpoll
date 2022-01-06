using System;
using System.Threading.Tasks;
using VkNet.Utils;
using VkNet.Model;
using VkNet.Abstractions;
using VkNet.Exception;

namespace VkNetLongpoll
{
    public class Longpoll
    {
        public bool isStarted { get; private set; } = false;

        private readonly ulong groupId;
        private readonly IVkApi api;
        private LongpollConnection connection;
        public LongpollEventHandler Handler = new LongpollEventHandler();
        public Longpoll(IVkApi api, long groupId)
        {
            if (!api.IsAuthorized)
                throw new ArgumentException("API must be authorized");

            this.api = api;
            this.groupId = (ulong)Math.Abs(groupId);
            connection = new LongpollConnection(ref this.api, this.groupId);
        }

        public async void Start() 
        {
            if (isStarted) return;

            isStarted = true;
            await connection.Create();
            await runPollingLoop();
        }
        public Task StartAsync() => Task.Run(Start);
        
        public void Stop()
        {
            isStarted = false;
        }

        private async Task runPollingLoop()
        {            
            while (isStarted)
            {
                BotsLongPollHistoryResponse response;
                try
                {
                    response = BotsLongPollHistoryResponse.FromJson(await api.CallLongPollAsync(connection.Server, connection.Params));
                }
                catch (LongPollOutdateException exception) 
                {
                    connection.Ts = exception.Ts;
                    continue;
                }
                catch (Exception exception) when (exception is LongPollKeyExpiredException || exception is LongPollInfoLostException)
                {
                    await connection.Create();
                    continue;
                }

                connection.Ts = response.Ts;
                foreach (var longpollEvent in response.Updates)
                    Handler?.Handle(longpollEvent, api);
            }
        }
    }

    class LongpollConnection
    {
        private readonly IVkApi api;
        private readonly ulong groupId;
        public ulong WaitMs = 25;
        public string Ts = null;
        public string Server = null;
        public string Key = null;
        public LongpollConnection(ref IVkApi api, ulong groupId)
        {
            this.api = api;
            this.groupId = groupId;
        }

        public async Task Create()
        {
            var response = await api.Groups.GetLongPollServerAsync(groupId);
            Ts = response.Ts;
            Key = response.Key;
            Server = response.Server;
        }
        public VkParameters Params
        {
            get => new VkParameters
            {
                { "ts", Ts }, { "key", Key }, { "act", "a_check" }, { "wait", Convert.ToString(WaitMs) }
            };
        }
    }
}
