namespace NexusPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusPulse.Application.Common.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly IAppDbContext _dbContext;

    public IncidentsController(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetIncidents([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var incidents = await _dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(i => i.StartedAt)
            .Take(limit)
            .ToListAsync(ct);
        return Ok(incidents);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIncidentById(Guid id, CancellationToken ct = default)
    {
        var incident = await _dbContext.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (incident == null) return NotFound();
        return Ok(incident);
    }
}
