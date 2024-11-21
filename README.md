# EventStore.PersistentSubscriptions.Demo

持久訂閱若因與 `EventStore` 連線暫時斷線重連，訂閱不會主動恢復訂閱

內建訂閱有提供實作 `SubscriptionDropped` 方法，故實做該方法發送事件，讓服務Retry重新連線

## EventHandler

訂閱歸訂閱，事件處理歸事件處理，故額外添加 `EventHandler` 將事件與處理綁定，並透過事件內的 `EventType` 判斷是哪種事件

來解析事件模型，及取得對應處理，這樣也能讓 `EventAppeared` 事件處理的實作統一，不用每個訂閱都寫一份

```csharp
// 繼承IEventStoreEvent宣告為EventStore的Event
public record CaseCreated :　IEventStoreEvent
{
    public string CaseId { get; set; }
}

// 繼承 IEventStoreEventHandler<IEventStoreEvent> 介面實作處理事件方法
public class CaseCreatedEventHandler : IEventStoreEventHandler<CaseCreated>
{
    public async Task HandleAsync(CaseCreated @event)
    {
        // TODO
    }
}

// DI添加事件處理
builder.Services.AddEventStoreEventHandler(options =>
{
    // 添加事件及其處理，利用EventType判斷
    options.Register<CaseCreated, CaseCreatedEventHandler>("EventType");
    // 動態添加，需要 EventModelName 與 EventTypeName 相同
    options.RegisterFromAssembly();
});
```

## EventStoreBackgroundService

由這邊添加要訂閱的串流，由於 `EventAppeared` 與 `SubscriptionDropped` 都能共用

故簡單包成 `PersistentSubscribeAsync` 避免重複 (可調整)

輸入 `<stream>::<group>` 就是 `subscriptionId` 

```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    // 持久訂閱串流
    await PersistentSubscribeAsync("$et-CaseCreated::QinGroup", cancellationToken);
}
```

## PersistentSubscriptionManager

額外多做非必要~ 

將過程封裝到 `PersistentSubscriptionManager` 持久訂閱管理器

於 `PersistentSubscriptionManagerBackgroundService` 內使用

每個訂閱都能獨立管控有自己的 `CancellationToken` ，開出介面後由 API 引用就能動態調整

**添加預設**
```csharp
await persistentSubscriptionManager.SubscribeAsync("$et-CaseCreated", "QinGroup");
```

**API動態調整**

```csharp
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
```