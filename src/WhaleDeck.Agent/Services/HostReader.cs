using System.Runtime.InteropServices;

namespace WhaleDeck.Agent.Services;

public sealed record HostSnapshot(
    string HostName,
    string Distribution,
    string Kernel,
    string Architecture,
    long BootTimeUnixSeconds,
    long UptimeSeconds,
    uint ProcessorCount,
    ulong TotalMemoryBytes);

public sealed class HostReader
{
    public HostSnapshot Read()
    {
        var uptime = ReadUptime();
        return new HostSnapshot(
            Environment.MachineName,
            ReadDistribution(),
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() - uptime,
            uptime,
            (uint)Environment.ProcessorCount,
            ReadTotalMemory());
    }

    public (double CpuPercent, double MemoryPercent) ReadUsage()
    {
        var memory = ReadMemoryInfo();
        var used = memory.Total == 0 ? 0 : (memory.Total - memory.Available) * 100d / memory.Total;
        return (ReadLoadAverage(), used);
    }

    private static long ReadUptime()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/uptime"))
        {
            return Environment.TickCount64 / 1000;
        }

        var value = File.ReadAllText("/proc/uptime").Split(' ', 2)[0];
        return long.TryParse(value.Split('.')[0], out var seconds) ? seconds : 0;
    }

    private static string ReadDistribution()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/etc/os-release"))
        {
            return RuntimeInformation.OSDescription;
        }

        var line = File.ReadLines("/etc/os-release").FirstOrDefault(item => item.StartsWith("PRETTY_NAME=", StringComparison.Ordinal));
        return line?["PRETTY_NAME=".Length..].Trim('"') ?? RuntimeInformation.OSDescription;
    }

    private static ulong ReadTotalMemory() => ReadMemoryInfo().Total;

    private static (ulong Total, ulong Available) ReadMemoryInfo()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/meminfo"))
        {
            var info = GC.GetGCMemoryInfo();
            return ((ulong)Math.Max(info.TotalAvailableMemoryBytes, 0), 0);
        }

        ulong total = 0;
        ulong available = 0;
        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !ulong.TryParse(parts[1], out var kilobytes))
            {
                continue;
            }

            if (parts[0] == "MemTotal:") total = kilobytes * 1024;
            if (parts[0] == "MemAvailable:") available = kilobytes * 1024;
        }
        return (total, available);
    }

    private static double ReadLoadAverage()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/loadavg"))
        {
            return 0;
        }
        var value = File.ReadAllText("/proc/loadavg").Split(' ', 2)[0];
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var load)
            ? Math.Min(load / Math.Max(Environment.ProcessorCount, 1) * 100, 100)
            : 0;
    }
}
