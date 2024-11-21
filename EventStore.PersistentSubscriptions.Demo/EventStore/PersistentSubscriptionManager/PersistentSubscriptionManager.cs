using EventStore.Client;
using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;
using System.Collections.Concurrent;
using Grpc.Core;
using Serilog;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.PersistentSubscriptionManager;

public class PersistentSubscriptionManager : IPersistentSubscriptionManager
{
    private readonly EventStorePersistentSubscriptionsClient _client;
    private readonly IEventHandlerInvoker _eventHandlerInvoker;
    private readonly IEventTypeMapper _eventTypeMapper;

    private readonly
        ConcurrentDictionary<string, (PersistentSubscription? persistentSubscription, CancellationTokenSource
            cancellationTokenSource)>
        _subscriptions = new();

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private event Func<string, Task>? SubscriptionDroppedEvent;
    private event Func<string, Task>? SubscriptionConnectedErrorEvent;
    private event Action<string, PersistentSubscription>? SubscribedSuccessfullyEvent;

    public PersistentSubscriptionManager(EventStorePersistentSubscriptionsClient client,
        IEventHandlerInvoker eventHandlerInvoker, IEventTypeMapper eventTypeMapper)
    {
        _client = client;
        _eventHandlerInvoker = eventHandlerInvoker;
        _eventTypeMapper = eventTypeMapper;

        Func<string, Task> resubscribeAsync = async subscriptionId =>
        {
            Log.Information($"嘗試重新訂閱：{subscriptionId}");
            Log.Information($"等待時間：{RetryDelay}");
            if (_subscriptions.TryGetValue(subscriptionId, out var subscription) == false)
            {
                Log.Warning($"Subscription Not Found - {subscriptionId}");
                return;
            }

            await Task.Delay(RetryDelay, subscription.cancellationTokenSource.Token);
            await PersistentSubscribeAsync(subscriptionId, subscription.cancellationTokenSource.Token);
        };
        SubscriptionDroppedEvent += resubscribeAsync;
        SubscriptionConnectedErrorEvent += resubscribeAsync;
        SubscribedSuccessfullyEvent += (subscriptionId, persistentSubscription) =>
        {
            if (_subscriptions.TryGetValue(subscriptionId, out var subscription) == false)
            {
                Log.Warning($"Subscription Not Found - {subscriptionId}");
            }

            _subscriptions.TryUpdate(subscriptionId, (persistentSubscription, subscription.cancellationTokenSource),
                subscription);
        };
    }

    public async Task SubscribeAsync(string steamName, string groupName)
    {
        if (_subscriptions.ContainsKey($"{steamName}::{groupName}"))
        {
            Log.Warning("Subscription Already Exists");
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _subscriptions.TryAdd($"{steamName}::{groupName}",
            (null, cancellationTokenSource));
        await PersistentSubscribeAsync($"{steamName}::{groupName}", cancellationTokenSource.Token);
    }

    public void Unsubscribe(string steamName, string groupName)
    {
        var subscriptionId = $"{steamName}::{groupName}";
        if (!_subscriptions.TryRemove(subscriptionId, out var subscription)) return;
        Log.Information($"取消訂閱：{subscriptionId}");
        subscription.persistentSubscription?.Dispose();
        subscription.cancellationTokenSource.Cancel();
    }

    public IList<string> GetSubscriptions()
    {
        return _subscriptions.Keys.ToList();
    }

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
                    await _client.ListToStreamAsync(stream, cancellationToken: cancellationToken);
                if (existingSubscriptions.Any(
                        x => x.Connections.Any(y => y.ConnectionName == _client.ConnectionName)))
                {
                    Log.Warning($"Subscription Already Connected - {stream}-{group}");
                    return;
                }
            }
            catch (PersistentSubscriptionNotFoundException)
            {
                Log.Warning($"Subscription Not Found - {stream}-{group}");
                return;
            }

            var subscription = await _client.SubscribeToStreamAsync(stream, group,
                eventAppeared: EventAppeared,
                subscriptionDropped: SubscriptionDropped,
                cancellationToken: cancellationToken);
            Log.Information($"訂閱成功：{subscription.SubscriptionId}");
            SubscribedSuccessfullyEvent?.Invoke(subscriptionId, subscription);
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
            var @event = _eventTypeMapper.Get(resolvedEvent);
            if (@event == null) return;
            await _eventHandlerInvoker.InvokeHandleAsync(@event);
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
        if (subscriptionDroppedReason != SubscriptionDroppedReason.Disposed)
        {
            SubscriptionDroppedEvent?.Invoke(persistentSubscription.SubscriptionId);
        }
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Value.persistentSubscription?.Dispose();
            subscription.Value.cancellationTokenSource.Cancel();
        }

        _client.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}