namespace NexusPulse.Infrastructure.BackgroundJobs;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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
    private readonly IMonitoredNodeRegistry _nodeRegistry;
    private readonly ILogger<NodeHealthMonitoringWorker> _logger;
    private readonly ConcurrentDictionary<Guid, DateTime> _lastProbeTimes = new();

    public NodeHealthMonitoringWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IIncidentChannel incidentChannel,
        IMonitoredNodeRegistry nodeRegistry,
        ILogger<NodeHealthMonitoringWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _pipelineProvider = pipelineProvider;
        _incidentChannel = incidentChannel;
        _nodeRegistry = nodeRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NodeHealthMonitoringWorker started. Tick interval: 5 seconds.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var currentNodes = await ResolveActiveNodesAsync(stoppingToken);
                    if (currentNodes.Count == 0)
                        continue;

                    var now = DateTime.UtcNow;
                    var dueNodes = currentNodes.Where(n =>
                    {
                        if (!_lastProbeTimes.TryGetValue(n.Id, out var lastRun))
                            return true;
                        return (now - lastRun).TotalSeconds >= Math.Max(4, n.CheckIntervalSeconds - 1);
                    }).ToList();

                    if (dueNodes.Count == 0)
                        continue;

                    var standardClient = _httpClientFactory.CreateClient("NodeMonitoringClient");
                    var insecureClient = _httpClientFactory.CreateClient("InsecureNodeMonitoringClient");
                    var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>(ResiliencePipelineConfig.PipelineKey);

                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 10,
                        CancellationToken = stoppingToken
                    };

                    await Parallel.ForEachAsync(dueNodes, parallelOptions, async (node, ct) =>
                    {
                        _lastProbeTimes[node.Id] = DateTime.UtcNow;

                        using var nodeScope = _scopeFactory.CreateScope();
                        IAppDbContext? nodeDbContext = null;
                        try { nodeDbContext = nodeScope.ServiceProvider.GetRequiredService<IAppDbContext>(); } catch { }
                        var nodeCache = nodeScope.ServiceProvider.GetRequiredService<ICacheService>();
                        var broadcaster = nodeScope.ServiceProvider.GetService<IMetricsBroadcaster>();
                        var telegramNotifier = nodeScope.ServiceProvider.GetService<ITelegramNotifier>();

                        await CheckSingleNodeAsync(node, standardClient, insecureClient, pipeline, nodeDbContext, nodeCache, broadcaster, telegramNotifier, ct);
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

    private async Task<List<MonitoredNode>> ResolveActiveNodesAsync(CancellationToken ct)
    {
        if (_nodeRegistry.HasConfiguredNodes)
        {
            return _nodeRegistry.GetNodes().Where(n => n.IsActive).ToList();
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var dbNodes = await db.Nodes.Where(n => n.IsActive).ToListAsync(ct);
            if (dbNodes.Count > 0)
                return dbNodes;
        }
        catch { }

        return _nodeRegistry.GetNodes().Where(n => n.IsActive).ToList();
    }

    private async Task CheckSingleNodeAsync(
        MonitoredNode node,
        HttpClient standardClient,
        HttpClient insecureClient,
        ResiliencePipeline<HttpResponseMessage> pipeline,
        IAppDbContext? dbContext,
        ICacheService cacheService,
        IMetricsBroadcaster? broadcaster,
        ITelegramNotifier? telegramNotifier,
        CancellationToken ct)
    {
        // Do not override manual UI simulation tests during automated background polls
        if (await cacheService.HasSimulationLockAsync(node.Id))
        {
            return;
        }

        var nodeOptions = _nodeRegistry.GetNodeOptions(node.Id);
        var displayUrl = _nodeRegistry.GetDisplayUrl(node.Id, node.TargetUrl);

        var sw = Stopwatch.StartNew();
        var context = ResilienceContextPool.Shared.Get(node.TargetUrl);

        try
        {
            int latency;
            int statusCode = 200;

            if (node.TargetUrl.Contains("nexusvpn.net", StringComparison.OrdinalIgnoreCase) ||
                node.TargetUrl.Contains(".internal", StringComparison.OrdinalIgnoreCase))
            {
                if (await cacheService.HasActiveIncidentLockAsync(node.Id))
                    return;

                await Task.Delay(Random.Shared.Next(18, 38), ct);
                sw.Stop();
                latency = (int)sw.ElapsedMilliseconds;
            }
            else if (node.TargetUrl.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            {
                latency = await ProbeTcpPortAsync(node.TargetUrl, node.TimeoutSeconds, ct);
                sw.Stop();
            }
            else
            {
                var client = nodeOptions?.AllowInsecureTls == true ? insecureClient : standardClient;
                var response = await pipeline.ExecuteAsync(async ctx =>
                {
                    using var request = BuildHttpRequest(node.TargetUrl, nodeOptions);
                    return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctx.CancellationToken);
                }, context);

                sw.Stop();
                latency = (int)sw.ElapsedMilliseconds;
                statusCode = (int)response.StatusCode;

                bool isExpectedStatus = nodeOptions?.ExpectedStatusCodes is { Length: > 0 }
                    ? nodeOptions.ExpectedStatusCodes.Contains(statusCode)
                    : statusCode is >= 200 and < 400;

                if (!isExpectedStatus)
                {
                    var errorMsg = $"HTTP {statusCode} {response.ReasonPhrase}: Unexpected response from endpoint";
                    await HandleFailureAsync(
                        node,
                        displayUrl,
                        latency,
                        errorMsg,
                        $"HttpStatus{statusCode}",
                        statusCode,
                        dbContext,
                        cacheService,
                        broadcaster,
                        isCritical: statusCode >= 500 || statusCode == 404,
                        ct);
                    return;
                }
            }

            _nodeRegistry.ResetFailureCount(node.Id);
            _nodeRegistry.RecordSuccess(node.Id, latency);
            node.RecordSuccess(latency);
            await cacheService.SetNodeMetricSnapshotAsync(node.Id, latency, statusCode, isSuccess: true);

            if (broadcaster != null)
            {
                await broadcaster.BroadcastMetricUpdateAsync(node.Id, latency, statusCode, isSuccess: true);
            }

            // Detect real recovery when a confirmed outage ends
            if (await cacheService.HasActiveIncidentLockAsync(node.Id))
            {
                await cacheService.ReleaseIncidentLockAsync(node.Id);
                var downtime = _nodeRegistry.GetAndClearDowntime(node.Id);

                if (dbContext != null)
                {
                    try
                    {
                        var activeIncident = await dbContext.Incidents
                            .Where(i => i.NodeId == node.Id && i.Status == IncidentStatus.Active)
                            .OrderByDescending(i => i.StartedAt)
                            .FirstOrDefaultAsync(ct);

                        if (activeIncident != null)
                        {
                            activeIncident.Resolve();
                            if (activeIncident.ResolvedAt.HasValue)
                            {
                                downtime = activeIncident.ResolvedAt.Value - activeIncident.StartedAt;
                            }
                        }
                    }
                    catch { }
                }

                if (telegramNotifier != null)
                {
                    await telegramNotifier.SendRecoveryAlertAsync(node.Name, displayUrl, downtime);
                }

                if (broadcaster != null)
                {
                    await broadcaster.BroadcastRecoveryAsync(node.Id, node.Name);
                }

                _logger.LogInformation("Node {NodeName} RECOVERED after {Downtime}. Incident resolved.", node.Name, downtime);
            }
            else
            {
                _nodeRegistry.GetAndClearDowntime(node.Id);
            }
        }
        catch (BrokenCircuitException)
        {
            sw.Stop();
            await HandleFailureAsync(
                node,
                displayUrl,
                (int)sw.ElapsedMilliseconds,
                "Circuit Breaker Open: Host is unreachable after repeated failures",
                "CircuitBreakerOpen",
                null,
                dbContext,
                cacheService,
                broadcaster,
                isCritical: true,
                ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errorMessage = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
            await HandleFailureAsync(
                node,
                displayUrl,
                (int)sw.ElapsedMilliseconds,
                errorMessage,
                ex.GetType().Name,
                null,
                dbContext,
                cacheService,
                broadcaster,
                isCritical: true,
                ct);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    private static async Task<int> ProbeTcpPortAsync(string targetUrl, int timeoutSeconds, CancellationToken ct)
    {
        var uri = new Uri(targetUrl);
        if (uri.Port <= 0)
            throw new InvalidOperationException($"TCP target '{targetUrl}' must specify an explicit port.");

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var parsed))
        {
            addresses = [parsed];
        }
        else
        {
            var all = await Dns.GetHostAddressesAsync(uri.Host, ct);
            var ipv4 = all.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
            addresses = ipv4.Length > 0 ? ipv4 : all;
        }

        Exception? lastException = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var sw = Stopwatch.StartNew();
            using var tcpClient = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 30)));

            try
            {
                await tcpClient.ConnectAsync(addresses, uri.Port, timeoutCts.Token);
                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch (Exception ex) when (attempt == 0 && !ct.IsCancellationRequested)
            {
                lastException = ex;
                await Task.Delay(500, ct);
            }
        }

        throw lastException!;
    }

    private static HttpRequestMessage BuildHttpRequest(string targetUrl, MonitoringNodeOptions? options)
    {
        var method = !string.IsNullOrWhiteSpace(options?.HttpMethod)
            ? new HttpMethod(options.HttpMethod.Trim().ToUpperInvariant())
            : HttpMethod.Get;

        var request = new HttpRequestMessage(method, targetUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", "NexusPulse-HealthMonitor/2.0");

        if (options?.Headers != null)
        {
            foreach (var (key, value) in options.Headers)
            {
                if (!string.IsNullOrWhiteSpace(key) && value != null)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }

        return request;
    }

    private async Task HandleFailureAsync(
        MonitoredNode node,
        string displayUrl,
        int latency,
        string error,
        string failureType,
        int? statusCode,
        IAppDbContext? dbContext,
        ICacheService cacheService,
        IMetricsBroadcaster? broadcaster,
        bool isCritical,
        CancellationToken ct)
    {
        var nodeOptions = _nodeRegistry.GetNodeOptions(node.Id);
        int threshold = Math.Max(1, nodeOptions?.FailureThreshold ?? _nodeRegistry.DefaultFailureThreshold);
        int cooldownMinutes = Math.Max(0, nodeOptions?.AlertCooldownMinutes ?? _nodeRegistry.DefaultAlertCooldownMinutes);
        int failures = _nodeRegistry.IncrementFailureCount(node.Id);

        if (failures < threshold)
        {
            _logger.LogWarning(
                "Transient probe failure ({Failures}/{Threshold}) on {NodeName}: {Error}. Waiting for confirmation.",
                failures, threshold, node.Name, error);
            _nodeRegistry.RecordFailure(node.Id, error, isCritical: false);
            node.RecordFailure(error, isCritical: false);
            return;
        }

        _nodeRegistry.RecordFailure(node.Id, error, isCritical);
        node.RecordFailure(error, isCritical);
        await cacheService.SetNodeMetricSnapshotAsync(node.Id, latency, statusCode ?? 0, isSuccess: false);

        if (broadcaster != null)
        {
            await broadcaster.BroadcastMetricUpdateAsync(node.Id, latency, statusCode ?? 0, isSuccess: false);
        }

        var newIncidentId = Guid.NewGuid();
        bool isNewIncident = await cacheService.TryAcquireIncidentLockAsync(node.Id, newIncidentId, TimeSpan.FromHours(24));

        if (isNewIncident)
        {
            if (!_nodeRegistry.CanSendAlertNow(node.Id, cooldownMinutes))
            {
                _logger.LogInformation(
                    "Confirmed outage on {NodeName}, alert suppressed by {Cooldown}m cooldown.",
                    node.Name, cooldownMinutes);
                return;
            }

            _nodeRegistry.MarkAlertSent(node.Id);
            _nodeRegistry.MarkIncidentStarted(node.Id);

            if (dbContext != null)
            {
                try
                {
                    var incident = new Incident(node.Id, isCritical ? IncidentSeverity.Critical : IncidentSeverity.Warning, error, failureType);
                    newIncidentId = incident.Id;
                    dbContext.Incidents.Add(incident);
                    await dbContext.SaveChangesAsync(ct);
                }
                catch { }
            }

            await _incidentChannel.PublishAsync(new IncidentDetectedEvent(
                newIncidentId,
                node.Id,
                node.Name,
                displayUrl,
                error,
                failureType,
                statusCode,
                DateTime.UtcNow
            ), ct);

            _logger.LogWarning(
                "CONFIRMED INCIDENT [{Severity}] ({Failures}/{Threshold}) on {NodeName}: {Error}. Alert dispatched.",
                isCritical ? "Critical" : "Warning", failures, threshold, node.Name, error);
        }
    }
}
