﻿using System;
using VkNet.Utils;
using VkNet.Model;
using VkNet.Exception;
using System.Threading;
using VkNet.Abstractions;
using System.Threading.Tasks;

namespace VkNetLongpoll
{
    public class Longpoll
    {
        public bool isStarted { get; private set; } = false;

        private readonly IVkApi api;
        private readonly ulong groupId;
        private LongpollConnection connection;
        public LongpollEventHandler Handler = new LongpollEventHandler();
        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        public Longpoll(IVkApi api, long groupId)
        {
            if (api == null)
                throw new ArgumentNullException(nameof(api));
            if (!api.IsAuthorized)
                throw new ArgumentException("API must be authorized");

            this.api = api;
            this.groupId = (ulong)Math.Abs(groupId);
            connection = new LongpollConnection(this.api, this.groupId);
        }
        public async Task Start() 
        {
            if (isStarted) return;
            await semaphoreSlim.WaitAsync();

            isStarted = true;
            try
            {
                await connection.Create();
                await runPollingLoop();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }        
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
                    response = BotsLongPollHistoryResponse.FromJson(await api.CallLongPollAsync(connection.Server, (VkParameters)connection));
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
                catch (Exception)
                {
                    throw;
                }

                connection.Ts = response.Ts;
                foreach (var update in response.Updates)
                    await Handler?.Handle(update);       
            }
        }
    }

    class LongpollConnection
    {
        public ulong WaitMs = 25;
        private readonly IVkApi api;
        private readonly ulong groupId;
        public string Ts { get; set; }
        public string Key { get; private set; }
        public string Server { get; private set; }
        public LongpollConnection(IVkApi api, ulong groupId)
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

        public static explicit operator VkParameters(LongpollConnection connection) => new VkParameters
            {
                { "act", "a_check" }, 
                { "ts", connection.Ts }, 
                { "key", connection.Key }, 
                { "wait", Convert.ToString(connection.WaitMs) }
            };
    }
}
