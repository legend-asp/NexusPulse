namespace NexusPulse.UnitTests.Resilience;

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusPulse.Infrastructure.Resilience;
using Polly.Registry;
using Xunit;

public class ResiliencePipelineTests
{
    [Fact]
    public async Task NodeMonitoringPipeline_When500InternalServerError_ShouldRetryTwice()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNodeMonitoringResilience();
        var provider = services.BuildServiceProvider();

        var pipelineProvider = provider.GetRequiredService<ResiliencePipelineProvider<string>>();
        var pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(ResiliencePipelineConfig.PipelineKey);

        int attempts = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async _ =>
        {
            attempts++;
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        // Assert: 1 initial attempt + 2 retries = 3 attempts total
        attempts.Should().Be(3);
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task NodeMonitoringPipeline_When200OK_ShouldNotRetry()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNodeMonitoringResilience();
        var provider = services.BuildServiceProvider();

        var pipelineProvider = provider.GetRequiredService<ResiliencePipelineProvider<string>>();
        var pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>(ResiliencePipelineConfig.PipelineKey);

        int attempts = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async _ =>
        {
            attempts++;
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Assert: only 1 attempt for successful responses
        attempts.Should().Be(1);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
