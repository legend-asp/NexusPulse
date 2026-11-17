namespace NexusPulse.Infrastructure.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using StackExchange.Redis;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCacheService> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> TryAcquireIncidentLockAsync(Guid nodeId, Guid incidentId, TimeSpan ttl)
    {
        string key = $"incident:active:{nodeId}";
        string value = incidentId.ToString();

        bool isSet = await _database.StringSetAsync(
            key: key,
            value: value,
            expiry: ttl,
            when: When.NotExists
        );

        if (isSet)
        {
            _logger.LogInformation("Active incident lock acquired for node {NodeId} with incident {IncidentId}", nodeId, incidentId);
        }
        else
        {
            _logger.LogDebug("Active incident lock already exists for node {NodeId}. Deduplication applied.", nodeId);
        }

        return isSet;
    }

    public async Task ReleaseIncidentLockAsync(Guid nodeId)
    {
        string key = $"incident:active:{nodeId}";
        await _database.KeyDeleteAsync(key);
        _logger.LogInformation("Released incident lock for node {NodeId}", nodeId);
    }

    public async Task<bool> HasActiveIncidentLockAsync(Guid nodeId)
    {
        string key = $"incident:active:{nodeId}";
        return await _database.KeyExistsAsync(key);
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

        await _database.StringSetAsync(key, json, TimeSpan.FromSeconds(60));
    }
}
