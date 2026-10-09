using WhaleDeck.Domain.Entities;

namespace WhaleDeck.UnitTests.Domain;

public sealed class AuditEventTests
{
    [Fact]
    public void NewAuditEventHasIdentityAndTimestamp()
    {
        var auditEvent = new AuditEvent
        {
            ActorSubject = "test-user",
            Action = "service.read",
            TargetType = "service",
            TargetId = "whaledeck",
            TraceId = "test-trace",
        };

        Assert.NotEqual(Guid.Empty, auditEvent.Id);
        Assert.True(auditEvent.OccurredAtUtc <= DateTimeOffset.UtcNow);
    }
}
