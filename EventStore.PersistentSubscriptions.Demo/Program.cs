using EventStore.Client;
using EventStore.PersistentSubscriptions.Demo.Event.CaseCreated;
using EventStore.PersistentSubscriptions.Demo.EventStore.BackgroundService;
using EventStore.PersistentSubscriptions.Demo.EventStore.DependicyInjection;
using EventStore.PersistentSubscriptions.Demo.EventStore.PersistentSubscriptionManager;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
// builder.Services.AddHostedService<EventStoreBackgroundService>();

builder.Services.AddSingleton<IPersistentSubscriptionManager, PersistentSubscriptionManager>();
builder.Services.AddHostedService<PersistentSubscriptionManagerBackgroundService>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();