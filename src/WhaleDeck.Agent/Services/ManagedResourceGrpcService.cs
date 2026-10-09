using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using WhaleDeck.Contracts.Agent.V1;

namespace WhaleDeck.Agent.Services;

public sealed class ManagedResourceGrpcService(ResourceRegistry registry, OperationStore operations, PlanStore plans)
    : ManagedResourceService.ManagedResourceServiceBase
{
    public override Task<ResourceCollectionResponse> ListDatabaseInstances(Empty request, ServerCallContext context) =>
        Task.FromResult(ToCollection(registry.All.Where(item => item.Id.StartsWith("database-platform.", StringComparison.Ordinal) && item.Type == "Container")));

    public override Task<ResourceCollectionResponse> ListSystemdUnits(Empty request, ServerCallContext context) =>
        Task.FromResult(ToCollection(registry.All.Where(item => item.Type == "SystemdUnit")));

    public override Task<ResourceCollectionResponse> ListComposeProjects(Empty request, ServerCallContext context) =>
        Task.FromResult(ToCollection(registry.All.Where(item => item.Type is "GitRepository" or "ManagedDirectory")));

    public override Task<ResourceSnapshot> GetConfigRepositoryStatus(RegisteredResourceRequest request, ServerCallContext context)
    {
        var resource = registry.Require(request.Resource.ResourceId, "status");
        var branch = RunReadOnlyGit(resource.Path!, "branch", "--show-current");
        var commit = RunReadOnlyGit(resource.Path!, "rev-parse", "HEAD");
        var status = RunReadOnlyGit(resource.Path!, "status", "--porcelain");
        var response = ToSnapshot(resource);
        response.State = string.IsNullOrWhiteSpace(status) ? "Clean" : "Dirty";
        response.Version = commit;
        response.Attributes["branch"] = branch;
        return Task.FromResult(response);
    }

    public override Task<OperationHandle> RunDatabaseAction(RegisteredActionRequest request, ServerCallContext context) =>
        Task.FromResult(QueueRegistered(request, "Database"));

    public override Task<OperationHandle> RunSystemdAction(RegisteredActionRequest request, ServerCallContext context) =>
        Task.FromResult(QueueRegistered(request, "Systemd"));

    public override Task<OperationHandle> RunComposeAction(RegisteredActionRequest request, ServerCallContext context) =>
        Task.FromResult(QueueRegistered(request, "Compose"));

    public override Task<OperationHandle> RunConfigRepositoryAction(RegisteredActionRequest request, ServerCallContext context) =>
        Task.FromResult(QueueRegistered(request, "ConfigRepository"));

    public override Task<OperationHandle> RunPlatformMaintenance(RegisteredActionRequest request, ServerCallContext context) =>
        Task.FromResult(QueueRegistered(request, "PlatformMaintenance", maintenance: true));

    private OperationHandle QueueRegistered(RegisteredActionRequest request, string category, bool maintenance = false)
    {
        registry.Require(request.Resource.ResourceId, request.Action);
        var idempotencyKey = BuildIdempotencyKey(request.Context, category, request.Action, request.Resource.ResourceId);
        var operation = operations.GetOrCreate(idempotencyKey, $"{category}:{request.Action}", out var created);
        if (!created) return operation;
        try
        {
            if (RequiresPlan(request.Action)) plans.VerifyAndConsume(request.PlanHash, request.Resource.ResourceId, request.Action, request.Parameters);
            operations.Save(operation, maintenance ? "Whale Deck 正在执行平台维护。" : null);
            return operation;
        }
        catch
        {
            operation.State = OperationState.Failed;
            operation.Phase = "ValidationFailed";
            operation.ErrorCode = "AGENT_ACTION_VALIDATION_FAILED";
            operations.Save(operation, maintenance ? "Whale Deck 平台维护预检失败。" : null);
            throw;
        }
    }

    private static string BuildIdempotencyKey(RequestContext? context, string category, string action, string resourceId) =>
        context is null || string.IsNullOrWhiteSpace(context.JobId)
            ? string.Empty
            : $"{context.JobId}:{context.IdempotencyKey}:{category}:{action}:{resourceId}";

    private static bool RequiresPlan(string action) => action is
        "delete" or "prune" or "apply-settings" or "install" or "update" or "reinstall" or "uninstall" or
        "update-install" or "reboot" or "commit-push" or "apply-update" or "rollback";

    private static ResourceCollectionResponse ToCollection(IEnumerable<RegisteredResource> resources)
    {
        var response = new ResourceCollectionResponse();
        response.Resources.AddRange(resources.Select(ToSnapshot));
        return response;
    }

    private static ResourceSnapshot ToSnapshot(RegisteredResource resource) => new()
    {
        ResourceId = resource.Id,
        DisplayName = resource.ExternalId,
        ResourceType = resource.Type,
        State = "Registered",
        Version = string.Empty,
        ProtectedResource = resource.ProtectionLevel != "Managed"
    };

    private static string RunReadOnlyGit(string path, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("Unable to start git.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(TimeSpan.FromSeconds(10));
        if (process.ExitCode != 0) throw new InvalidOperationException("Registered repository query failed.");
        return output.Trim();
    }
}
