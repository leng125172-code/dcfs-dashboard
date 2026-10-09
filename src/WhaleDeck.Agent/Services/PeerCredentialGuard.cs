using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Connections.Features;

namespace WhaleDeck.Agent.Services;

public sealed class PeerCredentialPolicy(IConfiguration configuration)
{
    private readonly uint _allowedGroupId = configuration.GetValue<uint>("Agent:AllowedPeerGid");

    public bool IsAllowed(uint userId, uint groupId) => userId == 0 || (_allowedGroupId > 0 && groupId == _allowedGroupId);
}

public sealed class PeerCredentialGuard(RequestDelegate next, ILogger<PeerCredentialGuard> logger)
{
    private const int SolSocket = 1;
    private const int SoPeerCred = 17;
    private static readonly Action<ILogger, uint, uint, Exception?> LogRejectedPeer = LoggerMessage.Define<uint, uint>(
        LogLevel.Warning,
        new EventId(2001, "PeerCredentialRejected"),
        "Rejected Agent UDS peer uid={PeerUid} gid={PeerGid}");

    public async Task InvokeAsync(HttpContext context, PeerCredentialPolicy policy)
    {
        var socket = context.Features.Get<IConnectionSocketFeature>()?.Socket;
        var credentials = default(PeerCredentials);
        if (socket is null || !TryGetPeerCredentials(socket, out credentials) || !policy.IsAllowed(credentials.UserId, credentials.GroupId))
        {
            LogRejectedPeer(logger, credentials.UserId, credentials.GroupId, null);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    private static bool TryGetPeerCredentials(Socket socket, out PeerCredentials credentials)
    {
        credentials = default;
        if (!OperatingSystem.IsLinux()) return false;
        var length = (uint)Marshal.SizeOf<PeerCredentials>();
        return getsockopt(socket.Handle.ToInt32(), SolSocket, SoPeerCred, out credentials, ref length) == 0 &&
               length == Marshal.SizeOf<PeerCredentials>();
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int getsockopt(int socket, int level, int optionName, out PeerCredentials optionValue, ref uint optionLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct PeerCredentials
    {
        public int ProcessId;
        public uint UserId;
        public uint GroupId;
    }
}
