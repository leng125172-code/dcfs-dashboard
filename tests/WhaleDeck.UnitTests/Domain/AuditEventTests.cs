using WhaleDeck.Domain.Entities;

namespace WhaleDeck.UnitTests.Domain;

public sealed class AuditEventTests
{
    [Fact]
    public void NewAuditEventHasIdentityAndTimestamp()
    {
        var auditEvent = new AuditEvent
        {
            ActorId = "test-user",
            Action = "service.read",
            Target = "whaledeck",
        };

        Assert.NotEqual(Guid.Empty, auditEvent.Id);
        Assert.True(auditEvent.OccurredAtUtc <= DateTimeOffset.UtcNow);
    }
}
