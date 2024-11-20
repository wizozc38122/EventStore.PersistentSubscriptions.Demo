namespace EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

public interface IEventStoreEventHandler<in TEvent> where TEvent : IEventStoreEvent 
{
    Task HandleAsync(TEvent @event);
}