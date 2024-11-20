using EventStore.Client;
using EventStore.PersistentSubscriptions.Demo.Event.CaseCreated;
using EventStore.PersistentSubscriptions.Demo.EventStore.BackgroundService;
using EventStore.PersistentSubscriptions.Demo.EventStore.DependicyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .MinimumLevel.Debug()
    .CreateLogger();
builder.Host.UseSerilog();

// 添加事件處理
builder.Services.AddEventStoreEventHandler(options =>
{
    // 添加事件及其處理，利用EventType判斷
    options.Register<CaseCreated, CaseCreatedEventHandler>(nameof(CaseCreated));
    // 動態添加，需要 EventModelName 與 EventTypeName 相同
    options.RegisterFromAssembly();
});

builder.Services.AddSingleton<EventStorePersistentSubscriptionsClient>(sp =>
{
    var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
    settings.ConnectivitySettings.KeepAliveTimeout = TimeSpan.FromSeconds(10);
    settings.ConnectivitySettings.KeepAliveInterval = TimeSpan.FromSeconds(5);
    // 設置永久重新連線
    settings.ConnectivitySettings.MaxDiscoverAttempts = -1;
    settings.ConnectivitySettings.DiscoveryInterval = TimeSpan.FromSeconds(10);
    return new EventStorePersistentSubscriptionsClient(settings);
});
builder.Services.AddHostedService<EventStoreBackgroundService>();

var app = builder.Build();
app.Run();