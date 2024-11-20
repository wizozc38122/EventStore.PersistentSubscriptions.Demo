using System.Text;
using System.Text.Json;
using EventStore.Client;

namespace EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;

public class EventTypeMapper : IEventTypeMapper
{
    private Dictionary<string, Type> _mapDict = new();

    public EventTypeMapper Map<TEvent>(string eventType) where TEvent : IEventStoreEvent
    {
        _mapDict[eventType] = typeof(TEvent);

        return this;
    }

    public IEventStoreEvent? Get(ResolvedEvent resolvedEvent)
    {
        var jsonData = Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);
        if (!_mapDict.TryGetValue(resolvedEvent.Event.EventType, out var map))
        {
            throw new Exception("Not Mapping");
        }

        return JsonSerializer.Deserialize(jsonData, map) as IEventStoreEvent;
    }
}