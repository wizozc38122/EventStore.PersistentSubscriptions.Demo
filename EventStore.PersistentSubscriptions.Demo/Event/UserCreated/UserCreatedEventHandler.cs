using EventStore.PersistentSubscriptions.Demo.EventStore.EventHandle;
using Serilog;

namespace EventStore.PersistentSubscriptions.Demo.Event.UserCreated;

public class UserCreatedEventHandler : IEventStoreEventHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated @event)
    {
        Log.Debug($"{nameof(UserCreated)}-{nameof(UserCreatedEventHandler)}-UserId：{@event.UserId}");
        await Task.CompletedTask;
    }
}