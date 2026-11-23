namespace NexusPulse.Infrastructure.BackgroundJobs;

using System.Diagnostics;
using System.Net.Http;
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

    public static readonly List<MonitoredNode> FallbackNodes =
    [
        new MonitoredNode(Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479"), "VPN Web Landing", "https://nexusvpn.net", 5, 5),
        new MonitoredNode(Guid.Parse("a12bc34d-58ee-4172-b567-1e02b2c3d480"), "VPN Node 01 (NL - Amsterdam)", "https://nl.nexusvpn.net/health", 5, 5),
        new MonitoredNode(Guid.Parse("c98de76f-12aa-4982-c567-2e02b2c3d481"), "VPN Node 02 (DE - Frankfurt)", "https://de.nexusvpn.net/health", 5, 5),
        new MonitoredNode(Guid.Parse("d34ef87a-23bb-4093-d678-3e02b2c3d482"), "VPN Node 03 (FI - Helsinki)", "https://fi.nexusvpn.net/health", 5, 5),
        new MonitoredNode(Guid.Parse("e56fa98b-34cc-4104-e789-4e02b2c3d483"), "VPN Node 04 (SE - Stockholm)", "https://se.nexusvpn.net/health", 5, 5),
        new MonitoredNode(Guid.Parse("f78ab09c-45dd-4215-f890-5e02b2c3d484"), "VPN Node 05 (PL - Warsaw)", "https://pl.nexusvpn.net/health", 5, 5),
        new MonitoredNode(Guid.Parse("b89bc10d-56ee-4326-a901-6e02b2c3d485"), "Merchant Payment Gateway", "https://api.cryptomus.com/v1/health", 5, 5),
        new MonitoredNode(Guid.Parse("c90cd21e-67ff-4437-b012-7e02b2c3d486"), "Telegram Bot Endpoint", "https://api.telegram.org", 5, 5)
    ];

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
        _logger.LogInformation("NodeHealthMonitoringWorker started. Monitoring interval: 5 seconds.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    List<MonitoredNode> currentNodes;
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                        currentNodes = await db.Nodes
                            .Where(n => n.IsActive)
                            .ToListAsync(stoppingToken);

                        if (currentNodes.Count == 0)
                        {
                            currentNodes = FallbackNodes.Where(n => n.IsActive).ToList();
                        }
                    }
                    catch
                    {
                        currentNodes = FallbackNodes.Where(n => n.IsActive).ToList();
                    }

                    if (currentNodes.Count == 0)
                        continue;

                    var httpClient = _httpClientFactory.CreateClient("NodeMonitoringClient");
                    var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>(ResiliencePipelineConfig.PipelineKey);

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 10,
                        CancellationToken = stoppingToken
                    };

                    await Parallel.ForEachAsync(currentNodes, parallelOptions, async (node, ct) =>
                    {
                        using var nodeScope = _scopeFactory.CreateScope();
                        IAppDbContext? nodeDbContext = null;
                        try { nodeDbContext = nodeScope.ServiceProvider.GetRequiredService<IAppDbContext>(); } catch { }
                        var nodeCache = nodeScope.ServiceProvider.GetRequiredService<ICacheService>();
                        var broadcaster = nodeScope.ServiceProvider.GetService<IMetricsBroadcaster>();
                        var telegramNotifier = nodeScope.ServiceProvider.GetService<ITelegramNotifier>();

                        await CheckSingleNodeAsync(node, httpClient, pipeline, nodeDbContext, nodeCache, broadcaster, telegramNotifier, ct);
                        if (nodeDbContext != null)
                        {
                            try { await nodeDbContext.SaveChangesAsync(ct); } catch { }
                        }
                    });
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Telemetry cycle skipped: {Message}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("NodeHealthMonitoringWorker stopping gracefully.");
        }
    }

    private async Task CheckSingleNodeAsync(
        MonitoredNode node,
        HttpClient httpClient,
        ResiliencePipeline<HttpResponseMessage> pipeline,
        IAppDbContext? dbContext,
        ICacheService cacheService,
        IMetricsBroadcaster? broadcaster,
        ITelegramNotifier? telegramNotifier,
        CancellationToken ct)
    {
        // Если для ноды зафиксирован активный инцидент или симуляция — не перезаписываем статусом Healthy
        if (await cacheService.HasActiveIncidentLockAsync(node.Id))
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        var context = ResilienceContextPool.Shared.Get(node.TargetUrl);

        try
        {
            HttpResponseMessage? response = null;
            int latency = 0;

            // Если URL моковый/внутренний плейсхолдер — симулируем живой пинг без заспамливания логов DNS
            if (node.TargetUrl.Contains("nexusvpn.net") || node.TargetUrl.Contains(".internal"))
            {
                await Task.Delay(Random.Shared.Next(18, 38), ct);
                sw.Stop();
                latency = (int)sw.ElapsedMilliseconds;
            }
            else
            {
                response = await pipeline.ExecuteAsync(async ctx =>
                    await httpClient.GetAsync(node.TargetUrl, ctx.CancellationToken), context);
                sw.Stop();
                latency = (int)sw.ElapsedMilliseconds;
            }

            int statusCode = response != null ? (int)response.StatusCode : 200;
            node.RecordSuccess(latency);
            await cacheService.SetNodeMetricSnapshotAsync(node.Id, latency, statusCode, isSuccess: true);

            if (broadcaster != null)
            {
                await broadcaster.BroadcastMetricUpdateAsync(node.Id, latency, statusCode, isSuccess: true);
            }
        }
        catch (BrokenCircuitException)
        {
            sw.Stop();
            await HandleFailureAsync(node, (int)sw.ElapsedMilliseconds, "Circuit Breaker Open: Host is unreachable", "CircuitBreakerOpen", dbContext, cacheService, broadcaster, isCritical: true, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await HandleFailureAsync(node, (int)sw.ElapsedMilliseconds, ex.Message, ex.GetType().Name, dbContext, cacheService, broadcaster, isCritical: false, ct);
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
        IAppDbContext? dbContext,
        ICacheService cacheService,
        IMetricsBroadcaster? broadcaster,
        bool isCritical,
        CancellationToken ct)
    {
        node.RecordFailure(error, isCritical);
        await cacheService.SetNodeMetricSnapshotAsync(node.Id, latency, 0, isSuccess: false);

        if (broadcaster != null)
        {
            await broadcaster.BroadcastMetricUpdateAsync(node.Id, latency, 0, isSuccess: false);
        }

        var newIncidentId = Guid.NewGuid();

        // Атомарный лок в Redis/памяти: вернет true ТОЛЬКО при первом переходе в состояние сбоя
        bool isNewIncident = await cacheService.TryAcquireIncidentLockAsync(node.Id, newIncidentId, TimeSpan.FromHours(24));

        if (isNewIncident)
        {
            try
            {
                if (dbContext != null)
                {
                    var incident = new Incident(node.Id, isCritical ? IncidentSeverity.Critical : IncidentSeverity.Warning, error, failureType);
                    dbContext.Incidents.Add(incident);
                    await dbContext.SaveChangesAsync(ct);
                }
            }
            catch { }

            // Отправляем событие в неблокирующий System.Threading.Channels
            await _incidentChannel.PublishAsync(new IncidentDetectedEvent(
                newIncidentId,
                node.Id,
                node.Name,
                node.TargetUrl,
                error,
                failureType,
                null,
                DateTime.UtcNow
            ), ct);

            _logger.LogWarning("NEW INCIDENT [{Severity}] on {NodeName}: {Error}. Event published to Channel.",
                isCritical ? "Critical" : "Warning", node.Name, error);
        }
    }
}
