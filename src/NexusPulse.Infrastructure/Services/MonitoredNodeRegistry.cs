namespace NexusPulse.Infrastructure.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;
using NexusPulse.Domain.Entities;

public sealed class MonitoredNodeRegistry : IMonitoredNodeRegistry
{
    private readonly ConcurrentDictionary<Guid, MonitoredNode> _nodes = new();
    private readonly ConcurrentDictionary<Guid, MonitoringNodeOptions> _options = new();
    private readonly ConcurrentDictionary<Guid, int> _latencies = new();
    private readonly ConcurrentDictionary<Guid, int> _consecutiveFailures = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastAlertTimes = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _incidentStartedTimes = new();
    private readonly List<Guid> _orderedIds = new();

    public bool HasConfiguredNodes { get; }
    public int DefaultFailureThreshold { get; }
    public int DefaultAlertCooldownMinutes { get; }

    public MonitoredNodeRegistry(IConfiguration configuration, ILogger<MonitoredNodeRegistry> logger)
    {
        var section = configuration.GetSection(MonitoringOptions.SectionName);
        var monitoringOptions = section.Get<MonitoringOptions>() ?? new MonitoringOptions();

        DefaultFailureThreshold = Math.Clamp(monitoringOptions.DefaultFailureThreshold, 1, 20);
        DefaultAlertCooldownMinutes = Math.Clamp(monitoringOptions.DefaultAlertCooldownMinutes, 0, 1440);

        if (monitoringOptions.Nodes.Count > 0)
        {
            HasConfiguredNodes = true;
            foreach (var opt in monitoringOptions.Nodes)
            {
                if (string.IsNullOrWhiteSpace(opt.Name) || string.IsNullOrWhiteSpace(opt.TargetUrl))
                    continue;

                var id = opt.GetDeterministicId();
                var entity = new MonitoredNode(
                    id,
                    opt.Name.Trim(),
                    opt.TargetUrl.Trim(),
                    Math.Max(5, opt.CheckIntervalSeconds),
                    Math.Clamp(opt.TimeoutSeconds, 1, 30));

                if (!opt.IsActive)
                {
                    entity.Deactivate();
                }

                _nodes[id] = entity;
                _options[id] = opt;
                _latencies[id] = 24;
                _orderedIds.Add(id);
            }

            logger.LogInformation(
                "Loaded {Count} configured monitoring targets (DefaultFailureThreshold={Threshold}, Cooldown={Cooldown}m).",
                _orderedIds.Count, DefaultFailureThreshold, DefaultAlertCooldownMinutes);
        }
        else
        {
            HasConfiguredNodes = false;
            foreach (var fallback in CreateDefaultDemoNodes())
            {
                _nodes[fallback.Id] = fallback;
                _latencies[fallback.Id] = 24;
                _orderedIds.Add(fallback.Id);
            }
        }
    }

    public IReadOnlyList<MonitoredNode> GetNodes()
    {
        var list = new List<MonitoredNode>(_orderedIds.Count);
        foreach (var id in _orderedIds)
        {
            if (_nodes.TryGetValue(id, out var node))
            {
                list.Add(node);
            }
        }
        return list;
    }

    public MonitoringNodeOptions? GetNodeOptions(Guid nodeId) =>
        _options.TryGetValue(nodeId, out var opt) ? opt : null;

    public string GetDisplayUrl(Guid nodeId, string fallbackUrl)
    {
        if (_options.TryGetValue(nodeId, out var opt) && !string.IsNullOrWhiteSpace(opt.DisplayUrl))
        {
            return opt.DisplayUrl;
        }
        return fallbackUrl;
    }

    public (string Name, string TargetUrl, string DisplayUrl) GetNodeInfo(Guid nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            return (node.Name, node.TargetUrl, GetDisplayUrl(nodeId, node.TargetUrl));
        }

        return ("Monitored Node", "https://internal.service/health", "https://internal.service/health");
    }

    public void RecordSuccess(Guid nodeId, int latencyMs)
    {
        _latencies[nodeId] = latencyMs;
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.RecordSuccess(latencyMs);
        }
    }

    public void RecordFailure(Guid nodeId, string error, bool isCritical)
    {
        _latencies[nodeId] = 0;
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.RecordFailure(error, isCritical);
        }
    }

    public int GetLastLatency(Guid nodeId, int defaultLatency = 24) =>
        _latencies.TryGetValue(nodeId, out var latency) ? latency : defaultLatency;

    public int IncrementFailureCount(Guid nodeId) =>
        _consecutiveFailures.AddOrUpdate(nodeId, 1, (_, current) => current + 1);

    public void ResetFailureCount(Guid nodeId) =>
        _consecutiveFailures.TryRemove(nodeId, out _);

    public bool CanSendAlertNow(Guid nodeId, int cooldownMinutes, DateTime? utcNow = null)
    {
        if (cooldownMinutes <= 0)
            return true;

        var now = utcNow ?? DateTime.UtcNow;
        if (_lastAlertTimes.TryGetValue(nodeId, out var lastSent))
        {
            return (now - lastSent) >= TimeSpan.FromMinutes(cooldownMinutes);
        }

        return true;
    }

    public void MarkAlertSent(Guid nodeId, DateTime? utcNow = null) =>
        _lastAlertTimes[nodeId] = utcNow ?? DateTime.UtcNow;

    public void MarkIncidentStarted(Guid nodeId, DateTime? utcNow = null) =>
        _incidentStartedTimes.TryAdd(nodeId, utcNow ?? DateTime.UtcNow);

    public TimeSpan GetAndClearDowntime(Guid nodeId, DateTime? utcNow = null)
    {
        if (_incidentStartedTimes.TryRemove(nodeId, out var startedAt))
        {
            var elapsed = (utcNow ?? DateTime.UtcNow) - startedAt;
            return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
        }
        return TimeSpan.Zero;
    }

    private static List<MonitoredNode> CreateDefaultDemoNodes() =>
    [
        new(Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479"), "VPN Web Landing", "https://nexusvpn.net", 5, 5),
        new(Guid.Parse("a12bc34d-58ee-4172-b567-1e02b2c3d480"), "VPN Node 01 (NL - Amsterdam)", "https://nl.nexusvpn.net/health", 5, 5),
        new(Guid.Parse("c98de76f-12aa-4982-c567-2e02b2c3d481"), "VPN Node 02 (DE - Frankfurt)", "https://de.nexusvpn.net/health", 5, 5),
        new(Guid.Parse("d34ef87a-23bb-4093-d678-3e02b2c3d482"), "VPN Node 03 (FI - Helsinki)", "https://fi.nexusvpn.net/health", 5, 5),
        new(Guid.Parse("e56fa98b-34cc-4104-e789-4e02b2c3d483"), "VPN Node 04 (SE - Stockholm)", "https://se.nexusvpn.net/health", 5, 5),
        new(Guid.Parse("f78ab09c-45dd-4215-f890-5e02b2c3d484"), "VPN Node 05 (PL - Warsaw)", "https://pl.nexusvpn.net/health", 5, 5),
        new(Guid.Parse("b89bc10d-56ee-4326-a901-6e02b2c3d485"), "Merchant Payment Gateway", "https://api.payments.internal/v1/charge", 5, 5),
        new(Guid.Parse("c90cd21e-67ff-4437-b012-7e02b2c3d486"), "Telegram Bot Endpoint", "https://api.telegram.org", 5, 5)
    ];
}
