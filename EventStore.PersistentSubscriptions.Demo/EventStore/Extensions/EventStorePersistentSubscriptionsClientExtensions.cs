using EventStore.Client;
using Grpc.Core;
using Serilog;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.Extensions;

public static class EventStorePersistentSubscriptionsClientExtensions
{
    /// <summary>
    /// 自動建立訂閱並訂閱至 Stream
    /// </summary>
    /// <param name="client"></param>
    /// <param name="streamName"></param>
    /// <param name="groupName"></param>
    /// <param name="eventAppeared"></param>
    /// <param name="settings"></param>
    /// <returns></returns>
    public static async Task<EventStorePersistentSubscriptionsClient> AutoCreateSubscribeToStreamAsync(
        this EventStorePersistentSubscriptionsClient client,
        string streamName, string groupName,
        Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
        ReSubscribeSettings settings)
    {
        await SubscribeAsync();
        return client;
        
        async Task SubscribeAsync()
        {
            var attempt = 0;
            while (settings.MaxReSubscribeAttempts == -1 || attempt < settings.MaxReSubscribeAttempts)
            {
                try
                {
                    if (settings.AutoCreatePersistentSubscription)
                    {
                        try
                        {
                            // 確認訂閱是否已存在
                            await client.GetInfoToStreamAsync(streamName, groupName);
                        }
                        catch (PersistentSubscriptionNotFoundException ex)
                        {
                            Log.Information($"建立訂閱：{streamName}-{groupName}");
                            await client.CreateToStreamAsync(
                                streamName,
                                groupName,
                                settings.PersistentSubscriptionSettings
                            );
                        }
                    }

                    // 避免重複消費訂閱
                    var checkAlreadySubscribed = await client.ListToStreamAsync(streamName);
                    if (checkAlreadySubscribed.Any(x =>
                            x.Connections.Any(y => y.ConnectionName == client.ConnectionName))) return;
                    await client.SubscribeToStreamAsync(streamName, groupName, eventAppeared: eventAppeared,
                        subscriptionDropped: SubscriptionDropped);
                    Log.Information($"訂閱成功：{streamName}-{groupName}");
                    break;
                }
                catch (RpcException)
                {
                    attempt++;
                    Log.Information($"嘗試重連...10s：{streamName}-{groupName}");
                    await Task.Delay(settings.ReSubscribeInterval);
                }
            }
        }

        void SubscriptionDropped(PersistentSubscription subscription, SubscriptionDroppedReason reason, Exception? ex)
        {
            Log.Information($"Subscription dropped. Reason: {reason}");
            subscription.Dispose();
            Task.Run(async () => await SubscribeAsync());
        }
    }
}

public class ReSubscribeSettings
{
    /// <summary>
    /// 持久訂閱設定
    /// </summary>
    public PersistentSubscriptionSettings PersistentSubscriptionSettings { get; init; } = new();

    /// <summary>
    /// 自動建立訂閱
    /// </summary>
    public bool AutoCreatePersistentSubscription { get; init; } = false;

    /// <summary>
    /// 重新訂閱最大嘗試次數
    /// </summary>
    public int MaxReSubscribeAttempts { get; init; } = 5;

    /// <summary>
    /// 重新訂閱間隔
    /// </summary>
    public TimeSpan ReSubscribeInterval { get; init; } = TimeSpan.FromSeconds(30);
}