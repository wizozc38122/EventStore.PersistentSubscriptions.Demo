using EventStore.Client;
using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;
using EventStore.PersistentSubscriptions.Demo.EventStore.Poller;
using Grpc.Core;
using Serilog;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.BackgroundService;

public class EventStoreBackgroundService(
    EventStorePersistentSubscriptionsClient client,
    IEventTypeMapper eventTypeMapper,
    IEventHandlerInvoker eventHandlerInvoker,
    IPoller<string> poller) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 訂閱
        poller.AddPending("$et-CaseCreated::QinGroup",
            () => AddPersistentSubscribePending("$et-CaseCreated", "QinGroup", cancellationToken));

        // 開始執行
        await poller.StartExecutionAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.DisposeAsync();
    }

    private async Task<PollStatus> AddPersistentSubscribePending(string stream, string group,
        CancellationToken cancellationToken)
    {
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
                    return PollStatus.Stop;
                }
            }
            catch (PersistentSubscriptionNotFoundException e)
            {
                Log.Warning($"Subscription Not Found - {stream}-{group}");
                return PollStatus.Stop;
            }

            var subscription = await client.SubscribeToStreamAsync("$et-CaseCreated", "QinGroup",
                eventAppeared: EventAppeared,
                subscriptionDropped: SubscriptionDropped,
                cancellationToken: cancellationToken);
            Log.Information($"訂閱成功：{subscription.SubscriptionId}");
            return PollStatus.Success;
        }
        catch (RpcException e)
        {
            Log.Warning($"Connection Error - {stream}-{group}");
            Log.Debug(e.Message);
            return PollStatus.Continue;
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
        poller.MoveToPending(persistentSubscription.SubscriptionId);
    }
}