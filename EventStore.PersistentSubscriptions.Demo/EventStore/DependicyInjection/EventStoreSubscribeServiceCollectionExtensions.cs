using System.Reflection;
using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.DependicyInjection;

public static class EventStoreSubscribeServiceCollectionExtensions
{
    public static IServiceCollection AddEventStoreEventHandler(
        this IServiceCollection services,
        Action<EventStoreEventHandlerSettings> set)
    {
        var eventTypeMapper = new EventTypeMapper();
        var settings = new EventStoreEventHandlerSettings(services, eventTypeMapper);
        set.Invoke(settings);
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
        services.AddSingleton<IEventTypeMapper>(eventTypeMapper);

        return services;
    }
}

public class EventStoreEventHandlerSettings
{
    private readonly IServiceCollection _services;
    private readonly EventTypeMapper _eventTypeMapper;

    internal EventStoreEventHandlerSettings(IServiceCollection services, EventTypeMapper eventTypeMapper)
    {
        _services = services;
        _eventTypeMapper = eventTypeMapper;
    }

    public EventStoreEventHandlerSettings Register<TEvent, TEventHandler>(string eventType)
        where TEvent : IEventStoreEvent
        where TEventHandler : class, IEventStoreEventHandler<TEvent>
    {
        _services.AddTransient<IEventStoreEventHandler<TEvent>, TEventHandler>();
        _eventTypeMapper.Map<TEvent>(eventType);
        return this;
    }

    public EventStoreEventHandlerSettings RegisterFromAssembly()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null) return this;
        
        // 找到所有實現 IEventStoreEventHandler<TEvent> 的類型
        var handlerTypes = assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventStoreEventHandler<>))
                .Select(i => new { HandlerType = type, EventType = i.GenericTypeArguments[0] }));

        foreach (var handler in handlerTypes)
        {
            // 取得事件類型名稱以作為註冊時的事件類型名稱
            var eventType = handler.EventType.Name;
            var method = typeof(EventStoreEventHandlerSettings)
                .GetMethod(nameof(Register))!
                .MakeGenericMethod(handler.EventType, handler.HandlerType);

            // 調用 Register<TEvent, TEventHandler> 註冊該事件和處理器
            method.Invoke(this, new object[] { eventType });
        }

        return this;
    }
}