namespace NexusPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusPulse.Domain.Entities;

public class MetricSnapshotConfiguration : IEntityTypeConfiguration<MetricSnapshot>
{
    public void Configure(EntityTypeBuilder<MetricSnapshot> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => new { m.NodeId, m.Timestamp });
    }
}
