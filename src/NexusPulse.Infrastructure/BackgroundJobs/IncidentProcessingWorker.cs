namespace NexusPulse.Infrastructure.BackgroundJobs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Application.Common.Models;

public class IncidentProcessingWorker : BackgroundService
{
    private readonly IIncidentChannel _incidentChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncidentProcessingWorker> _logger;

    public IncidentProcessingWorker(
        IIncidentChannel incidentChannel,
        IServiceScopeFactory scopeFactory,
        ILogger<IncidentProcessingWorker> logger)
    {
        _incidentChannel = incidentChannel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IncidentProcessingWorker started. Listening for incident events...");

        try
        {
            await foreach (var incidentEvent in _incidentChannel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessIncidentAsync(incidentEvent, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing incident {IncidentId} for node {NodeName}",
                        incidentEvent.IncidentId, incidentEvent.NodeName);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("IncidentProcessingWorker gracefully stopping.");
        }
    }

    private async Task ProcessIncidentAsync(IncidentDetectedEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var aiDiagnosticService = scope.ServiceProvider.GetRequiredService<IAiDiagnosticService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var telegramNotifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>();
        var broadcaster = scope.ServiceProvider.GetService<IMetricsBroadcaster>();

        _logger.LogWarning("Processing incident {IncidentId} on node {NodeName}. Requesting AI diagnosis...",
            evt.IncidentId, evt.NodeName);

        // 1. Diagnose with Google Gemini AI
        AiDiagnosis? diagnosis = null;
        try
        {
            diagnosis = await aiDiagnosticService.DiagnoseIncidentAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain AI diagnosis for incident {IncidentId}", evt.IncidentId);
        }

        // 2. Persist diagnosis to PostgreSQL (resilient fallback if DB offline)
        try
        {
            var incident = await dbContext.Incidents.FirstOrDefaultAsync(i => i.Id == evt.IncidentId, ct);
            if (incident != null && diagnosis != null)
            {
                incident.AttachAiDiagnosis(diagnosis.RootCause, diagnosis.ImmediateFix, diagnosis.Prevention);
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Attached AI diagnosis to incident {IncidentId} in database.", evt.IncidentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist incident to DB (database may be offline). Proceeding to alert dispatch.");
        }

        // 3. Dispatch alert to Telegram
        try
        {
            await telegramNotifier.SendIncidentAlertAsync(evt, diagnosis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch Telegram alert for incident {IncidentId}", evt.IncidentId);
        }

        // 4. Broadcast live via SignalR to React dashboard
        if (broadcaster != null)
        {
            try
            {
                await broadcaster.BroadcastIncidentAsync(evt, diagnosis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast incident {IncidentId} via SignalR", evt.IncidentId);
            }
        }
    }
}
