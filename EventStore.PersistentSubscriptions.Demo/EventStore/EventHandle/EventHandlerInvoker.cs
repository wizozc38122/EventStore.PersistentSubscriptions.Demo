using System.Collections.Concurrent;
using System.Reflection;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

public class EventHandlerInvoker : IEventHandlerInvoker
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly ConcurrentDictionary<Type, MethodInfo?> MethodCache = new();

    public EventHandlerInvoker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeHandleAsync<TEvent>(TEvent @event) where TEvent : IEventStoreEvent
    {
        try
        {
            var eventHandlerType = typeof(IEventStoreEventHandler<>).MakeGenericType(@event.GetType());

            var eventHandler = _serviceProvider.GetService(eventHandlerType);
            if (eventHandler is null) return;

            var method = MethodCache.GetOrAdd(eventHandlerType, type =>
                type.GetMethod(nameof(IEventStoreEventHandler<IEventStoreEvent>.HandleAsync)));
            if (method is null) return;

            await (method.Invoke(eventHandler, [@event]) as Task);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}