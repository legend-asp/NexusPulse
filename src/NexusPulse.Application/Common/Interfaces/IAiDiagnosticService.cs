namespace NexusPulse.Application.Common.Interfaces;

using NexusPulse.Application.Common.Models;

public interface IAiDiagnosticService
{
    Task<AiDiagnosis> DiagnoseIncidentAsync(IncidentDetectedEvent incidentEvent, CancellationToken cancellationToken = default);
}
