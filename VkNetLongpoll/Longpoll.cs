using System;
using VkNet.Utils;
using VkNet.Model;
using VkNet.Exception;
using System.Threading;
using VkNet.Abstractions;
using VkNetLongpoll.Handlers;
using System.Threading.Tasks;
using VkNet.Model.GroupUpdate;
using System.Collections.Generic;

namespace VkNetLongpoll;
public class Longpoll
{
    public bool IsStarted 
    { 
        get => cts != null && !cts.IsCancellationRequested; 
    }

    private readonly IVkApi api;
    private readonly ulong groupId;
    private CancellationTokenSource cts;
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
        if (IsStarted) return;
        await semaphoreSlim.WaitAsync();

        cts = new CancellationTokenSource();
        try
        {
            await connection.Create();
            await runPollingLoop(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }        
    public void Stop()
    {
        cts.Cancel();
    }
    private async Task runPollingLoop(CancellationToken token)
    {            
        while (!token.IsCancellationRequested)
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
            finally
            {
                token.ThrowIfCancellationRequested();
            }
            
            connection.Ts = response.Ts;
            await handleUpdates(response.Updates, token);
        }
    }
    private async Task handleUpdates(IEnumerable<GroupUpdate> updates, CancellationToken token)
    {
        foreach (var update in updates)
        {
            token.ThrowIfCancellationRequested();
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