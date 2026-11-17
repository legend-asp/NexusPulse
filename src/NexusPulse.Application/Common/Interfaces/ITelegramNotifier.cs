namespace NexusPulse.Application.Common.Interfaces;

using NexusPulse.Application.Common.Models;

public interface ITelegramNotifier
{
    Task SendIncidentAlertAsync(IncidentDetectedEvent incidentEvent, AiDiagnosis? diagnosis = null);
    Task SendRecoveryAlertAsync(string nodeName, string targetUrl, TimeSpan downtime);
}
