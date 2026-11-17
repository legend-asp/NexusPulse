namespace NexusPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusPulse.Domain.Entities;

public class MonitoredNodeConfiguration : IEntityTypeConfiguration<MonitoredNode>
{
    public void Configure(EntityTypeBuilder<MonitoredNode> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Name).HasMaxLength(100).IsRequired();
        builder.Property(n => n.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(n => n.HealthStatus).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(n => n.TargetUrl);
    }
}
