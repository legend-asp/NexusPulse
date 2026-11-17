namespace NexusPulse.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Domain.Entities;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MonitoredNode> Nodes => Set<MonitoredNode>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<MetricSnapshot> Metrics => Set<MetricSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
