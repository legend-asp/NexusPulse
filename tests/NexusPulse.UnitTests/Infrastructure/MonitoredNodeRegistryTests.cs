namespace NexusPulse.UnitTests.Infrastructure;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NexusPulse.Infrastructure.Services;
using Xunit;

public class MonitoredNodeRegistryTests
{
    private static MonitoredNodeRegistry CreateRegistry()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Monitoring:DefaultFailureThreshold"] = "3",
            ["Monitoring:DefaultAlertCooldownMinutes"] = "10"
        }).Build();

        return new MonitoredNodeRegistry(config, NullLogger<MonitoredNodeRegistry>.Instance);
    }

    [Fact]
    public void AntiFlapping_FailureCountAndCooldown_ShouldSuppressRapidReAlerts()
    {
        var registry = CreateRegistry();
        var nodeId = Guid.NewGuid();
        var t0 = DateTime.UtcNow;

        registry.IncrementFailureCount(nodeId).Should().Be(1);
        registry.IncrementFailureCount(nodeId).Should().Be(2);
        registry.ResetFailureCount(nodeId);
        registry.IncrementFailureCount(nodeId).Should().Be(1);

        registry.CanSendAlertNow(nodeId, 10, t0).Should().BeTrue();
        registry.MarkAlertSent(nodeId, t0);
        registry.CanSendAlertNow(nodeId, 10, t0.AddMinutes(5)).Should().BeFalse();
        registry.CanSendAlertNow(nodeId, 10, t0.AddMinutes(11)).Should().BeTrue();
    }

    [Fact]
    public void DowntimeTracking_ShouldComputeElapsedDuration()
    {
        var registry = CreateRegistry();
        var nodeId = Guid.NewGuid();
        var t0 = DateTime.UtcNow;

        registry.MarkIncidentStarted(nodeId, t0);
        var downtime = registry.GetAndClearDowntime(nodeId, t0.AddSeconds(95));

        downtime.TotalSeconds.Should().BeApproximately(95, 0.1);
        registry.GetAndClearDowntime(nodeId, t0.AddSeconds(120)).Should().Be(TimeSpan.Zero);
    }
}
