namespace NexusPulse.Application.Common.Interfaces;

using Microsoft.EntityFrameworkCore;
using NexusPulse.Domain.Entities;

public interface IAppDbContext
{
    DbSet<MonitoredNode> Nodes { get; }
    DbSet<Incident> Incidents { get; }
    DbSet<MetricSnapshot> Metrics { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
