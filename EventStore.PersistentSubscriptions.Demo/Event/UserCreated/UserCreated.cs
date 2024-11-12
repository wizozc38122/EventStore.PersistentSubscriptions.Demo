using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

namespace EventStore.PersistentSubscriptions.Demo.Event.UserCreated;

public class UserCreated : IEventStoreEvent
{
    public string UserId { get; set; }
}