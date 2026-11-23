namespace NexusPulse.UnitTests.Domain;

using FluentAssertions;
using NexusPulse.Domain.Entities;
using NexusPulse.Domain.Enums;
using Xunit;

public class MonitoredNodeTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var node = new MonitoredNode("Billing API", "https://api.billing.com/health", checkIntervalSeconds: 15, timeoutSeconds: 3);

        // Assert
        node.Name.Should().Be("Billing API");
        node.TargetUrl.Should().Be("https://api.billing.com/health");
        node.CheckIntervalSeconds.Should().Be(15);
        node.TimeoutSeconds.Should().Be(3);
        node.HealthStatus.Should().Be(NodeStatus.Healthy);
        node.IsActive.Should().BeTrue();
        node.LastCheckedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_ShouldThrowArgumentException(string invalidName)
    {
        // Act
        Action act = () => new MonitoredNode(invalidName, "https://api.example.com/health");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Node name cannot be empty*");
    }

    [Theory]
    [InlineData("invalid-url")]
    [InlineData("ftp:/missing-slash")]
    public void Constructor_WithInvalidUrl_ShouldThrowArgumentException(string invalidUrl)
    {
        // Act
        Action act = () => new MonitoredNode("Valid Name", invalidUrl);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Valid absolute URL is required*");
    }

    [Fact]
    public void RecordSuccess_ShouldSetStatusToHealthy_AndRecordTimestamp()
    {
        // Arrange
        var node = new MonitoredNode("Payment Gateway", "https://pay.example.com/health");

        // Act
        node.RecordSuccess(responseTimeMs: 42);

        // Assert
        node.HealthStatus.Should().Be(NodeStatus.Healthy);
        node.LastCheckedAt.Should().NotBeNull();
        node.LastCheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordFailure_WhenCritical_ShouldSetStatusToDown()
    {
        // Arrange
        var node = new MonitoredNode("Database Cluster", "https://db.example.com/health");

        // Act
        node.RecordFailure("Connection refused: 500", isCritical: true);

        // Assert
        node.HealthStatus.Should().Be(NodeStatus.Down);
        node.LastCheckedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordFailure_WhenNonCritical_ShouldSetStatusToDegraded()
    {
        // Arrange
        var node = new MonitoredNode("Auth Service", "https://auth.example.com/health");

        // Act
        node.RecordFailure("Latency spike: 2500ms", isCritical: false);

        // Assert
        node.HealthStatus.Should().Be(NodeStatus.Degraded);
        node.LastCheckedAt.Should().NotBeNull();
    }
}
