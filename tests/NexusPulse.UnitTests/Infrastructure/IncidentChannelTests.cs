namespace NexusPulse.UnitTests.Infrastructure;

using FluentAssertions;
using NexusPulse.Application.Common.Models;
using NexusPulse.Infrastructure.Services;
using Xunit;

public class IncidentChannelTests
{
    [Fact]
    public async Task PublishAsync_ShouldAllowReadingFromChannel_WithoutDeadlocks()
    {
        // Arrange
        var channel = new IncidentChannel();
        var incidentEvent = new IncidentDetectedEvent(
            IncidentId: Guid.NewGuid(),
            NodeId: Guid.NewGuid(),
            NodeName: "Test Node",
            TargetUrl: "https://test.internal/health",
            ErrorMessage: "Gateway Timeout 504",
            FailureType: "HttpTimeout",
            StatusCode: 504,
            OccurredAt: DateTime.UtcNow
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act
        await channel.PublishAsync(incidentEvent, cts.Token);

        // Assert
        var reader = channel.ReadAllAsync(cts.Token);
        var enumerator = reader.GetAsyncEnumerator(cts.Token);

        var moveNext = await enumerator.MoveNextAsync();
        moveNext.Should().BeTrue();
        enumerator.Current.Should().NotBeNull();
        enumerator.Current.IncidentId.Should().Be(incidentEvent.IncidentId);
        enumerator.Current.NodeName.Should().Be("Test Node");
        enumerator.Current.StatusCode.Should().Be(504);
    }
}
