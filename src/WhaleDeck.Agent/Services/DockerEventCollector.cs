using Docker.DotNet.Models;

namespace WhaleDeck.Agent.Services;

public sealed class DockerEventCollector(
    DockerEngine docker,
    DockerEventBuffer buffer,
    ILogger<DockerEventCollector> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogReconnect = LoggerMessage.Define<int>(
        LogLevel.Warning,
        new EventId(1201, "DockerEventStreamReconnect"),
        "Docker event stream disconnected; reconnecting in {DelaySeconds} seconds");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delaySeconds = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var progress = new Progress<Message>(buffer.Add);
                await docker.MonitorEventsAsync(progress, stoppingToken);
                delaySeconds = 1;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogReconnect(logger, delaySeconds, exception);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                delaySeconds = Math.Min(delaySeconds * 2, 30);
            }
        }
    }
}
