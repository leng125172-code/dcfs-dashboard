using Dcfs.Dashboard.Worker;
using Serilog;
using Serilog.Formatting.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter()));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
