using System.Net.NetworkInformation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using WhaleDeck.Contracts.Agent.V1;

namespace WhaleDeck.Agent.Services;

public sealed class HostGrpcService(HostReader host, OperationStore operations) : HostService.HostServiceBase
{
    public override Task<HostInfoResponse> GetHostInfo(Empty request, ServerCallContext context)
    {
        var snapshot = host.Read();
        return Task.FromResult(new HostInfoResponse
        {
            HostName = snapshot.HostName,
            Distribution = snapshot.Distribution,
            KernelVersion = snapshot.Kernel,
            Architecture = snapshot.Architecture,
            BootTimeUnixSeconds = snapshot.BootTimeUnixSeconds,
            UptimeSeconds = snapshot.UptimeSeconds,
            LogicalProcessorCount = snapshot.ProcessorCount,
            TotalMemoryBytes = snapshot.TotalMemoryBytes
        });
    }

    public override async Task StreamMetrics(MetricsRequest request, IServerStreamWriter<MetricPoint> responseStream, ServerCallContext context)
    {
        var singleBatch = request.IntervalSeconds == 0;
        var interval = TimeSpan.FromSeconds(Math.Clamp(request.IntervalSeconds, 6, 60));
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var usage = host.ReadUsage();
            var timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
            await responseStream.WriteAsync(new MetricPoint { MetricKind = "cpu.utilization", DeviceId = "host", Value = usage.CpuPercent, Unit = "percent", Quality = "Good", SampledAtUtc = timestamp });
            await responseStream.WriteAsync(new MetricPoint { MetricKind = "memory.utilization", DeviceId = "host", Value = usage.MemoryPercent, Unit = "percent", Quality = "Good", SampledAtUtc = timestamp });
            foreach (var metric in host.ReadDeviceMetrics())
            {
                await responseStream.WriteAsync(new MetricPoint { MetricKind = metric.Kind, DeviceId = metric.DeviceId, Value = metric.Value, Unit = metric.Unit, Quality = "Good", SampledAtUtc = timestamp });
            }
            await responseStream.WriteAsync(new MetricPoint { MetricKind = "batch.complete", DeviceId = "host", Unit = "none", Quality = "Good", SampledAtUtc = timestamp });
            if (singleBatch) return;
            await Task.Delay(interval, context.CancellationToken);
        }
    }

    public override Task<ResourceCollectionResponse> ListNetworkInterfaces(Empty request, ServerCallContext context)
    {
        var response = new ResourceCollectionResponse();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            var snapshot = new ResourceSnapshot
            {
                ResourceId = $"network-interface:{adapter.Id}",
                DisplayName = adapter.Name,
                ResourceType = "NetworkInterface",
                State = adapter.OperationalStatus.ToString(),
                Version = adapter.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            snapshot.Attributes["type"] = adapter.NetworkInterfaceType.ToString();
            snapshot.Attributes["description"] = adapter.Description;
            response.Resources.Add(snapshot);
        }
        return Task.FromResult(response);
    }

    public override Task<ResourceCollectionResponse> ListStorageDevices(Empty request, ServerCallContext context)
    {
        var response = new ResourceCollectionResponse();
        foreach (var drive in DriveInfo.GetDrives().Where(item => item.IsReady))
        {
            var snapshot = new ResourceSnapshot
            {
                ResourceId = $"filesystem:{drive.Name}",
                DisplayName = drive.Name,
                ResourceType = "Filesystem",
                State = "Ready",
                Version = drive.DriveFormat
            };
            snapshot.Attributes["totalBytes"] = drive.TotalSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            snapshot.Attributes["availableBytes"] = drive.AvailableFreeSpace.ToString(System.Globalization.CultureInfo.InvariantCulture);
            response.Resources.Add(snapshot);
        }
        return Task.FromResult(response);
    }

    public override Task<OperationHandle> RebootHost(RegisteredActionRequest request, ServerCallContext context)
    {
        if (!string.Equals(request.Action, "reboot", StringComparison.OrdinalIgnoreCase))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Only the reboot action is supported."));
        }
        var operation = operations.Create("HostRebootQueued");
        operations.Save(operation, "工作站正在准备重启。");
        return Task.FromResult(operation);
    }
}
