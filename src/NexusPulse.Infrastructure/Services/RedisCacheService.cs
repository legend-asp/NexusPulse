namespace NexusPulse.Infrastructure.Services;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using StackExchange.Redis;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly ConcurrentDictionary<Guid, Guid> _inMemoryIncidentLocks = new();
    private static readonly ConcurrentDictionary<Guid, DateTime> _inMemorySimulationLocks = new();
    private static readonly ConcurrentDictionary<Guid, string> _inMemoryMetrics = new();

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCacheService> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> TryAcquireIncidentLockAsync(Guid nodeId, Guid incidentId, TimeSpan ttl)
    {
        string key = $"incident:active:{nodeId}";
        string value = incidentId.ToString();

        if (_database.Multiplexer.IsConnected)
        {
            try
            {
                bool isSet = await _database.StringSetAsync(
                    key: key,
                    value: value,
                    expiry: ttl,
                    when: When.NotExists
                );

                if (isSet)
                {
                    _inMemoryIncidentLocks[nodeId] = incidentId;
                    _logger.LogInformation("Active incident lock acquired in Redis for node {NodeId} with incident {IncidentId}", nodeId, incidentId);
                }
                return isSet;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Redis offline ({Message}), using in-memory incident lock for node {NodeId}", ex.Message, nodeId);
            }
        }

        return _inMemoryIncidentLocks.TryAdd(nodeId, incidentId);
    }

    public async Task ReleaseIncidentLockAsync(Guid nodeId)
    {
        string key = $"incident:active:{nodeId}";
        if (_database.Multiplexer.IsConnected)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
                _logger.LogInformation("Released incident lock in Redis for node {NodeId}", nodeId);
            }
            catch { }
        }

        _inMemoryIncidentLocks.TryRemove(nodeId, out _);
    }

    public async Task<bool> HasActiveIncidentLockAsync(Guid nodeId)
    {
        string key = $"incident:active:{nodeId}";
        if (_database.Multiplexer.IsConnected)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch { }
        }

        return _inMemoryIncidentLocks.ContainsKey(nodeId);
    }

    public async Task SetSimulationLockAsync(Guid nodeId, TimeSpan ttl)
    {
        string key = $"incident:sim:{nodeId}";
        _inMemorySimulationLocks[nodeId] = DateTime.UtcNow.Add(ttl);
        if (_database.Multiplexer.IsConnected)
        {
            try
            {
                await _database.StringSetAsync(key, "1", ttl);
            }
            catch { }
        }
    }

    public async Task<bool> HasSimulationLockAsync(Guid nodeId)
    {
        string key = $"incident:sim:{nodeId}";
        if (_database.Multiplexer.IsConnected)
        {
            try
            {
                return await _database.KeyExistsAsync(key);
            }
            catch { }
        }

        if (_inMemorySimulationLocks.TryGetValue(nodeId, out var expiresAt))
        {
            if (expiresAt > DateTime.UtcNow)
                return true;
            _inMemorySimulationLocks.TryRemove(nodeId, out _);
        }
        return false;
    }

    public async Task ReleaseSimulationLockAsync(Guid nodeId)
    {
        string key = $"incident:sim:{nodeId}";
        _inMemorySimulationLocks.TryRemove(nodeId, out _);
        if (_database.Multiplexer.IsConnected)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
            }
            catch { }
        }
    }

    public async Task SetNodeMetricSnapshotAsync(Guid nodeId, int responseTimeMs, int statusCode, bool isSuccess)
    {
        string key = $"metrics:latest:{nodeId}";
        var snapshot = new
        {
            ResponseTimeMs = responseTimeMs,
            StatusCode = statusCode,
            IsSuccess = isSuccess,
            UpdatedAt = DateTime.UtcNow
        };
        string json = JsonSerializer.Serialize(snapshot);

        _inMemoryMetrics[nodeId] = json;
        if (_database.Multiplexer.IsConnected)
        {
            try
            {
                await _database.StringSetAsync(key, json, TimeSpan.FromSeconds(60));
            }
            catch { }
        }
    }
}
