namespace WhaleDeck.Agent.Services;

public sealed class SocketPermissionService(IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var socketPath = configuration["Agent:SocketPath"] ?? "/run/whaledeck/agent.sock";
        while (!stoppingToken.IsCancellationRequested && !File.Exists(socketPath))
        {
            await Task.Delay(50, stoppingToken);
        }

        if (!OperatingSystem.IsWindows() && File.Exists(socketPath))
        {
            File.SetUnixFileMode(socketPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
