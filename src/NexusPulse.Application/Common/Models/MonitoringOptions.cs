namespace NexusPulse.Application.Common.Models;

using System.Security.Cryptography;
using System.Text;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";
    public int DefaultFailureThreshold { get; set; } = 3;
    public int DefaultAlertCooldownMinutes { get; set; } = 10;
    public List<MonitoringNodeOptions> Nodes { get; set; } = new();
}

public sealed class MonitoringNodeOptions
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string? DisplayUrl { get; set; }
    public int CheckIntervalSeconds { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 5;
    public int? FailureThreshold { get; set; }
    public int? AlertCooldownMinutes { get; set; }
    public bool AllowInsecureTls { get; set; } = false;
    public string HttpMethod { get; set; } = "GET";
    public int[]? ExpectedStatusCodes { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid GetDeterministicId()
    {
        if (Id.HasValue && Id.Value != Guid.Empty)
            return Id.Value;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{Name.Trim()}|{TargetUrl.Trim()}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
