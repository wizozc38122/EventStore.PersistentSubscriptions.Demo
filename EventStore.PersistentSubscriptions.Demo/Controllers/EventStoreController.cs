using EventStore.PersistentSubscriptions.Demo.EventStore.PersistentSubscriptionManager;
using Microsoft.AspNetCore.Mvc;

namespace EventStore.PersistentSubscriptions.Demo.Controllers;

[ApiController]
[Route("[controller]")]
public class EventStoreController(IPersistentSubscriptionManager persistentSubscriptionManager) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(persistentSubscriptionManager.GetSubscriptions());
    }
    
    [HttpPost]
    public IActionResult Post(string streamName, string groupName)
    {
        persistentSubscriptionManager.SubscribeAsync(streamName, groupName);
        return Ok();
    }
    
    [HttpDelete]
    public IActionResult Delete(string streamName, string groupName)
    {
        persistentSubscriptionManager.Unsubscribe(streamName, groupName);
        return Ok();
    }
}