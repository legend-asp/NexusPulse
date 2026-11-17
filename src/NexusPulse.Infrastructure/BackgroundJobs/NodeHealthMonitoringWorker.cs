using System.Net.Http;
namespace NexusPulse.Infrastructure.BackgroundJobs;

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;
using NexusPulse.Domain.Entities;
using NexusPulse.Domain.Enums;
using NexusPulse.Infrastructure.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

public sealed class NodeHealthMonitoringWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly IIncidentChannel _incidentChannel;
    private readonly ILogger<NodeHealthMonitoringWorker> _logger;

    public NodeHealthMonitoringWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IIncidentChannel incidentChannel,
        ILogger<NodeHealthMonitoringWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _pipelineProvider = pipelineProvider;
        _incidentChannel = incidentChannel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NodeHealthMonitoringWorker started. Interval: 5s");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                var nodes = await dbContext.Nodes
                    .Where(n => n.IsActive)
                    .ToListAsync(stoppingToken);

                if (nodes.Count == 0)
                    continue;

                var httpClient = _httpClientFactory.CreateClient("NodeMonitoringClient");
                var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>(ResiliencePipelineConfig.PipelineKey);

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 10,
                    CancellationToken = stoppingToken
                };

                await Parallel.ForEachAsync(nodes, parallelOptions, async (node, ct) =>
                {
                    await CheckSingleNodeAsync(node, httpClient, pipeline, dbContext, cacheService, ct);
                });

                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NodeHealthMonitoringWorker stopping gracefully.");
        }
    }

    private async Task CheckSingleNodeAsync(
        MonitoredNode node,
        HttpClient httpClient,
        ResiliencePipeline<HttpResponseMessage> pipeline,
        IAppDbContext dbContext,
        ICacheService cacheService,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var context = ResilienceContextPool.Shared.Get(node.TargetUrl);

        try
        {
            var response = await pipeline.ExecuteAsync(async ctx =>
                await httpClient.GetAsync(node.TargetUrl, ctx.CancellationToken), context);

            sw.Stop();
            int latency = (int)sw.ElapsedMilliseconds;

            node.RecordSuccess(latency);
            await cacheService.SetNodeMetricSnapshotAsync(node.Id, latency, (int)response.StatusCode, isSuccess: true);

            // Если нода ранее лежала, значит она ожила!
            if (await cacheService.HasActiveIncidentLockAsync(node.Id))
            {
                await cacheService.ReleaseIncidentLockAsync(node.Id);
                var activeIncident = await dbContext.Incidents
                    .Where(i => i.NodeId == node.Id && i.Status == IncidentStatus.Active)
                    .OrderByDescending(i => i.StartedAt)
                    .FirstOrDefaultAsync(ct);

                activeIncident?.Resolve();
                _logger.LogInformation("Node {NodeName} RECOVERED. Incident resolved.", node.Name);
            }
        }
        catch (BrokenCircuitException)
        {
            sw.Stop();
            await HandleFailureAsync(node, (int)sw.ElapsedMilliseconds, "Circuit Breaker Open: Host is unreachable", "CircuitBreakerOpen", dbContext, cacheService, isCritical: true, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await HandleFailureAsync(node, (int)sw.ElapsedMilliseconds, ex.Message, ex.GetType().Name, dbContext, cacheService, isCritical: false, ct);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private async Task HandleFailureAsync(
        MonitoredNode node,
        int latency,
        string error,
        string failureType,
        IAppDbContext dbContext,
        ICacheService cacheService,
        bool isCritical,
        CancellationToken ct)
    {
        node.RecordFailure(error, isCritical);
        await cacheService.SetNodeMetricSnapshotAsync(node.Id, latency, 0, isSuccess: false);

        var newIncidentId = Guid.NewGuid();

        // Атомарный лок в Redis: вернет true ТОЛЬКО при первом падении
        bool isNewIncident = await cacheService.TryAcquireIncidentLockAsync(node.Id, newIncidentId, TimeSpan.FromHours(24));

        if (isNewIncident)
        {
            var incident = new Incident(node.Id, isCritical ? IncidentSeverity.Critical : IncidentSeverity.Warning, error, failureType);
            dbContext.Incidents.Add(incident);

            // Отправляем в неблокирующий Channel
            await _incidentChannel.PublishAsync(new IncidentDetectedEvent(
                incident.Id,
                node.Id,
                node.Name,
                node.TargetUrl,
                error,
                failureType,
                null,
                DateTime.UtcNow
            ), ct);

            _logger.LogWarning("NEW INCIDENT detected for node {NodeName} ({Url}). Published to channel.", node.Name, node.TargetUrl);
        }
    }
}

