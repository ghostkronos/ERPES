using System.Threading.Channels;
using ERPES.ControlTower.Core.DTOs;
using ERPES.ControlTower.Core.Entities;
using ERPES.ControlTower.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERPES.ControlTower.Controllers;

[ApiController]
[Route("api/v1/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        ApplicationDbContext dbContext,
        ILogger<TelemetryController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("/api/v1/telemetry/ingest")]
    public async Task<IActionResult> IngestData(
        [FromBody] TelemetryDto payload,
        [FromServices] ChannelWriter<TelemetryDto> channel)
    {
        _logger.LogInformation("Telemetry payload queued: Source={Source}, Event={Event}",
            payload.SourceType, payload.EventType);

        await channel.WriteAsync(payload, HttpContext.RequestAborted);
        return Accepted(new IngestResultDto());
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<List<ExternalTelemetryAlert>>> GetAlerts([FromQuery] bool? unresolvedOnly)
    {
        var query = _dbContext.ExternalTelemetryAlerts
            .Include(a => a.TargetPlant)
            .AsQueryable();

        if (unresolvedOnly == true)
        {
            query = query.Where(a => !a.IsResolved);
        }

        var alerts = await query.OrderByDescending(a => a.CreatedAt).Take(20).ToListAsync();
        return Ok(alerts);
    }
}
