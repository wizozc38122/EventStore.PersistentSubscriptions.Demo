using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

namespace EventStore.PersistentSubscriptions.Demo.Event.CaseCreated;

public record CaseCreated :　IEventStoreEvent
{
    public string CaseId { get; set; }
}