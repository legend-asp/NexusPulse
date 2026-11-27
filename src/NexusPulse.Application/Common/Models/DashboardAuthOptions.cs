namespace NexusPulse.Application.Common.Models;

public sealed class DashboardAuthOptions
{
    public const string SectionName = "DashboardAuth";

    public bool Enabled { get; set; } = false;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
    public string TotpSecretBase32 { get; set; } = string.Empty;
    public string SessionSecret { get; set; } = string.Empty;
    public int SessionLifetimeHours { get; set; } = 24;
}
