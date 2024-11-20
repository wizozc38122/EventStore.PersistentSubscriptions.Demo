using EventStore.Client;
using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;
using Grpc.Core;
using Serilog;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.BackgroundService;

public class EventStoreBackgroundService(
    EventStorePersistentSubscriptionsClient client,
    IEventTypeMapper eventTypeMapper,
    IEventHandlerInvoker eventHandlerInvoker) : IHostedService
{
    /// <summary>
    /// 重試延遲
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private event Func<string, Task>? SubscriptionDroppedEvent;
    private event Func<string, Task>? SubscriptionConnectedErrorEvent;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 持久訂閱串流
        await PersistentSubscribeAsync("$et-CaseCreated::QinGroup", cancellationToken);

        // 重新訂閱事件處理
        Func<string, Task> resubscribeAsync = async subscriptionId =>
        {
            Log.Information($"嘗試重新訂閱：{subscriptionId}");
            Log.Information($"等待時間：{RetryDelay}s");
            await Task.Delay(RetryDelay, cancellationToken);
            await PersistentSubscribeAsync(subscriptionId, cancellationToken);
        };
        SubscriptionDroppedEvent += resubscribeAsync;
        SubscriptionConnectedErrorEvent += resubscribeAsync;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.DisposeAsync();
    }

    /// <summary>
    /// 持久訂閱串流
    /// </summary>
    /// <param name="subscriptionId"></param>
    /// <param name="cancellationToken"></param>
    private async Task PersistentSubscribeAsync(string subscriptionId,
        CancellationToken cancellationToken)
    {
        var stream = subscriptionId.Split("::")[0];
        var group = subscriptionId.Split("::")[1];
        try
        {
            try
            {
                var existingSubscriptions =
                    await client.ListToStreamAsync(stream, cancellationToken: cancellationToken);
                if (existingSubscriptions.Any(
                        x => x.Connections.Any(y => y.ConnectionName == client.ConnectionName)))
                {
                    Log.Warning($"Subscription Already Connected - {stream}-{group}");
                    return;
                }
            }
            catch (PersistentSubscriptionNotFoundException e)
            {
                Log.Warning($"Subscription Not Found - {stream}-{group}");
                return;
            }

            var subscription = await client.SubscribeToStreamAsync("$et-CaseCreated", "QinGroup",
                eventAppeared: EventAppeared,
                subscriptionDropped: SubscriptionDropped,
                cancellationToken: cancellationToken);
            Log.Information($"訂閱成功：{subscription.SubscriptionId}");
        }
        catch (RpcException e)
        {
            Log.Warning($"Connection Error - {stream}-{group}");
            Log.Debug(e.Message);
            SubscriptionConnectedErrorEvent?.Invoke(subscriptionId);
        }
    }

    private async Task EventAppeared(PersistentSubscription persistentSubscription, ResolvedEvent resolvedEvent,
        int? retryCount, CancellationToken cancellationToken)
    {
        try
        {
            Log.Debug($"""
                       EventAppeared
                       StreamId: {resolvedEvent.Event.EventStreamId}
                       EventId: {resolvedEvent.Event.EventId}
                       EventType: {resolvedEvent.Event.EventType}
                       EventNumber: {resolvedEvent.Event.EventNumber}
                       """);
            var @event = eventTypeMapper.Get(resolvedEvent);
            if (@event == null) return;
            await eventHandlerInvoker.InvokeHandleAsync(@event);
            await persistentSubscription.Ack(resolvedEvent);
        }
        catch (Exception e)
        {
            await persistentSubscription.Nack
            (
                action: PersistentSubscriptionNakEventAction.Park,
                reason: e.Message,
                resolvedEvent
            );
            Log.Warning(e.Message);
        }
    }

    private void SubscriptionDropped(PersistentSubscription persistentSubscription,
        SubscriptionDroppedReason subscriptionDroppedReason, Exception? exception)
    {
        persistentSubscription.Dispose();
        Log.Warning($"{exception?.Message} - {subscriptionDroppedReason}");
        SubscriptionDroppedEvent?.Invoke(persistentSubscription.SubscriptionId);
    }
}