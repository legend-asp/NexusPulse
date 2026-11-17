namespace NexusPulse.Infrastructure.Services;

using System.Threading.Channels;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;

public class IncidentChannel : IIncidentChannel
{
    private readonly Channel<IncidentDetectedEvent> _channel = Channel.CreateBounded<IncidentDetectedEvent>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    public ValueTask PublishAsync(IncidentDetectedEvent incidentEvent, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(incidentEvent, cancellationToken);

    public IAsyncEnumerable<IncidentDetectedEvent> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
