using WhaleDeck.Worker;
using WhaleDeck.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Services(services)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter()));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
