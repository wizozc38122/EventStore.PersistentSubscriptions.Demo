using EventStore.PersistentSubscriptions.Demo.EventStore.PersistentSubscriptionManager;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.BackgroundService;

public class PersistentSubscriptionManagerBackgroundService(
    IPersistentSubscriptionManager persistentSubscriptionManager) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await persistentSubscriptionManager.SubscribeAsync("$et-CaseCreated", "QinGroup");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await persistentSubscriptionManager.DisposeAsync();
    }
}