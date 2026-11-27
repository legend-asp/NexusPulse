namespace NexusPulse.Infrastructure.Resilience;

using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

public static class ResiliencePipelineConfig
{
    public const string PipelineKey = "node-monitoring-pipeline";

    public static IServiceCollection AddNodeMonitoringResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline<string, HttpResponseMessage>(PipelineKey, (builder, context) =>
        {
            // 1. Retry with exponential backoff & jitter
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            });

            // 2. Circuit Breaker isolated per key (host or nodeId)
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1.0,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            });

            // 3. Timeout per request
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(8)
            });
        });

        return services;
    }
}
