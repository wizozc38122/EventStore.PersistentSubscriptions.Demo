namespace EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

public interface IEventHandlerInvoker
{
    Task InvokeHandleAsync<TEvent>(TEvent @event) where TEvent : IEventStoreEvent;
}