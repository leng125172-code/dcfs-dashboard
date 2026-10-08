using WhaleDeck.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActorId).HasMaxLength(256);
            entity.Property(item => item.Action).HasMaxLength(128);
            entity.Property(item => item.Target).HasMaxLength(512);
            entity.HasIndex(item => item.OccurredAtUtc);
        });

        modelBuilder.Entity<PlatformSetting>(entity =>
        {
            entity.ToTable("platform_settings");
            entity.HasKey(item => item.Key);
            entity.Property(item => item.Key).HasMaxLength(256);
            entity.Property(item => item.ValueJson).HasColumnType("jsonb");
        });
    }
}
