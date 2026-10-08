using Dcfs.Dashboard.Domain.Entities;

namespace Dcfs.Dashboard.UnitTests.Domain;

public sealed class AuditEventTests
{
    [Fact]
    public void NewAuditEventHasIdentityAndTimestamp()
    {
        var auditEvent = new AuditEvent
        {
            ActorId = "test-user",
            Action = "service.read",
            Target = "dashboard",
        };

        Assert.NotEqual(Guid.Empty, auditEvent.Id);
        Assert.True(auditEvent.OccurredAtUtc <= DateTimeOffset.UtcNow);
    }
}
