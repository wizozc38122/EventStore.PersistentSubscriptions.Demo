using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;
using Serilog;

namespace EventStore.PersistentSubscriptions.Demo.Event.CaseCreated;

public class CaseCreatedEventHandler : IEventStoreEventHandler<CaseCreated>
{
    public async Task HandleAsync(CaseCreated @event)
    {
        Log.Debug($"{nameof(CaseCreated)}-{nameof(CaseCreatedEventHandler)}-CaseId：{@event.CaseId}");
        await Task.CompletedTask;
    }
}