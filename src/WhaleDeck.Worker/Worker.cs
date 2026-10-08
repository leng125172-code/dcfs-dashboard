namespace WhaleDeck.Worker;

public partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "WhaleDeck worker started")]
    private static partial void LogStarted(ILogger logger);
}
