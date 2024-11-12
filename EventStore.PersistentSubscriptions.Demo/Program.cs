using EventStore.Client;
using EventStore.PersistentSubscriptions.Demo.EventStore.BackgroundService;
using EventStore.PersistentSubscriptions.Demo.EventStore.DependicyInjection;
using EventStore.PersistentSubscriptions.Demo.EventStore.Poller;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .MinimumLevel.Debug()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddEventStoreEventHandler(options =>
{
    options.RegisterFromAssembly();
});

builder.Services.AddSingleton<EventStorePersistentSubscriptionsClient>(sp =>
{
    var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
    settings.ConnectivitySettings.KeepAliveTimeout = TimeSpan.FromSeconds(10);
    settings.ConnectivitySettings.KeepAliveInterval = TimeSpan.FromSeconds(5);
    settings.ConnectivitySettings.MaxDiscoverAttempts = -1;
    settings.ConnectivitySettings.DiscoveryInterval = TimeSpan.FromSeconds(10);
    return new EventStorePersistentSubscriptionsClient(settings);
});
builder.Services.AddSingleton<IPoller<string>, Poller<string>>();
builder.Services.AddHostedService<EventStoreBackgroundService>();

var app = builder.Build();
app.Run();