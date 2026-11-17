namespace NexusPulse.Application.Common.Interfaces;

using NexusPulse.Application.Common.Models;

public interface IIncidentChannel
{
    ValueTask PublishAsync(IncidentDetectedEvent incidentEvent, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IncidentDetectedEvent> ReadAllAsync(CancellationToken cancellationToken = default);
}
