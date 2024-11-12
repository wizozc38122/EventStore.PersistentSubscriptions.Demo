using EventStore.Client;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

public interface IEventTypeMapper
{
    IEventStoreEvent? Get(ResolvedEvent resolvedEvent);
}