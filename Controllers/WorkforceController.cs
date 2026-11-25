using ERPES.ControlTower.Core.DTOs;
using ERPES.ControlTower.Core.Entities;
using ERPES.ControlTower.Hubs;
using ERPES.ControlTower.Infrastructure.Data;
using ERPES.ControlTower.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ERPES.ControlTower.Controllers;

[ApiController]
[Route("api/v1/workforce")]
public class WorkforceController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPrescriptiveDecisionEngine _decisionEngine;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WorkforceController> _logger;

    public WorkforceController(
        ApplicationDbContext dbContext,
        IPrescriptiveDecisionEngine decisionEngine,
        IHubContext<TelemetryHub> hubContext,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<WorkforceController> logger)
    {
        _dbContext = dbContext;
        _decisionEngine = decisionEngine;
        _hubContext = hubContext;
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetWorkforceOverview()
    {
        var plants = await _dbContext.Plants.ToListAsync();
        var allWorkers = await _dbContext.Workers
            .Include(w => w.HomePlant)
            .Include(w => w.CurrentPlant)
            .OrderByDescending(w => w.IsMobilityAccepted)
            .ThenByDescending(w => w.SkillScore)
            .ToListAsync();

        var transferOrders = await _dbContext.WorkerTransferOrders
            .Include(o => o.Worker)
            .Include(o => o.FromPlant)
            .Include(o => o.ToPlant)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var roles = allWorkers.Select(w => w.RoleOrSkill).Distinct().OrderBy(r => r).ToList();

        return Ok(new
        {
            plants,
            workers = allWorkers,
            transferOrders,
            roles
        });
    }

    private async Task<Plant?> ResolvePlantAsync(int plantId)
    {
        var p = await _dbContext.Plants.FindAsync(plantId);
        if (p != null) return p;

        if (plantId == 1) return await _dbContext.Plants.FirstOrDefaultAsync(x => x.Code == "PLANT-A");
        if (plantId == 2) return await _dbContext.Plants.FirstOrDefaultAsync(x => x.Code == "PLANT-B");
        if (plantId == 3) return await _dbContext.Plants.FirstOrDefaultAsync(x => x.Code == "PLANT-C");
        return null;
    }

    /// <summary>
    /// [Architect] LINQ Workforce Optimization Endpoint:
    /// Ranks eligible mobile workers by minimal transfer cost & highest skill score without causing deficit at donor plant.
    /// </summary>
    [HttpPost("optimize")]
    public async Task<IActionResult> OptimizeWorkforceAllocation([FromBody] WorkerTransferRequestDto request)
    {
        var targetPlant = await ResolvePlantAsync(request.TargetPlantId);
        if (targetPlant == null)
            return NotFound(new { error = "Hedef fabrika bulunamadı." });

        int duration = Math.Max(1, request.DurationDays);

        int neededCount = Math.Max(1, request.DeficitCount);
        var eligiblePool = await _dbContext.Workers
            .Include(w => w.CurrentPlant)
            .Include(w => w.HomePlant)
            .Where(w => w.CurrentPlantId != targetPlant.Id)
            .Where(w => w.IsMobilityAccepted == true && w.Status == "Active")
            .Where(w => w.CurrentPlant != null && w.CurrentPlant.CurrentWorkerCount > w.CurrentPlant.RequiredWorkerCount)
            .Where(w => string.IsNullOrEmpty(request.RequiredRoleOrSkill) ||
                        request.RequiredRoleOrSkill == "Tümü" ||
                        w.RoleOrSkill.Contains(request.RequiredRoleOrSkill))
            .OrderBy(w => w.MobilityAllowanceRate)
            .ThenByDescending(w => w.SkillScore)
            .ToListAsync();

        var candidates = new List<Worker>();
        var remainingSurplusByPlant = new Dictionary<int, int>();
        foreach (var worker in eligiblePool)
        {
            if (candidates.Count >= neededCount) break;
            if (worker.CurrentPlant == null) continue;

            if (!remainingSurplusByPlant.TryGetValue(worker.CurrentPlantId, out int surplus))
            {
                surplus = worker.CurrentPlant.CurrentWorkerCount - worker.CurrentPlant.RequiredWorkerCount;
            }
            if (surplus <= 0) continue;

            remainingSurplusByPlant[worker.CurrentPlantId] = surplus - 1;
            candidates.Add(worker);
        }

        var rankedResults = candidates.Select(w =>
        {
            decimal travelCost = 200m;
            decimal totalCost = (w.MobilityAllowanceRate * duration) + travelCost;
            decimal preventedPenalty = 15_000m;
            return new
            {
                worker = w,
                durationDays = duration,
                dailyAllowance = w.MobilityAllowanceRate,
                travelCost,
                totalEstimatedCost = totalCost,
                preventedPenaltyAmount = preventedPenalty,
                netRoi = preventedPenalty - totalCost
            };
        }).ToList();

        return Ok(new
        {
            targetPlant = targetPlant.Name,
            requiredRole = request.RequiredRoleOrSkill,
            requestedCount = request.DeficitCount,
            matchedCount = rankedResults.Count,
            totalCost = rankedResults.Sum(r => r.totalEstimatedCost),
            totalPreventedPenalty = rankedResults.Sum(r => r.preventedPenaltyAmount),
            candidates = rankedResults
        });
    }

    /// <summary>
    /// Direct 1-Click Worker Dispatch with strict QA enforcement of IsMobilityAccepted == true
    /// </summary>
    [HttpPost("dispatch")]
    public async Task<IActionResult> DispatchWorker([FromBody] DirectWorkerDispatchDto dto)
    {
        var worker = await _dbContext.Workers
            .Include(w => w.CurrentPlant)
            .FirstOrDefaultAsync(w => w.Id == dto.WorkerId);

        if (worker == null)
            return NotFound(new { error = "Personel bulunamadı." });

        if (!worker.IsMobilityAccepted)
        {
            return BadRequest(new
            {
                error = $"Sözleşme Kısıtı: {worker.FullName} isimli personelin sözleşmesi sabit kadro statüsünde olduğu için başka bir fabrikaya görevlendirilemez!"
            });
        }

        if (worker.Status != "Active")
        {
            return BadRequest(new
            {
                error = $"{worker.FullName} şu anda aktif görevlendirilebilir durumda değil (Durum: {worker.Status})."
            });
        }

        var fromPlant = await ResolvePlantAsync(worker.CurrentPlantId);
        var toPlant = await ResolvePlantAsync(dto.ToPlantId);
        if (fromPlant == null || toPlant == null)
            return BadRequest(new { error = "Kaynak veya hedef fabrika geçersiz." });

        if (worker.CurrentPlantId == toPlant.Id)
        {
            return BadRequest(new { error = "Personel zaten seçilen hedef fabrikada görev yapmaktadır." });
        }

        if (fromPlant.CurrentWorkerCount <= fromPlant.RequiredWorkerCount)
        {
            return BadRequest(new
            {
                error = $"Kadro Kısıtı: {fromPlant.Name} tesisinde fazladan personel bulunmadığı için ({fromPlant.CurrentWorkerCount}/{fromPlant.RequiredWorkerCount}) görevlendirme yapılamaz."
            });
        }

        int days = Math.Max(1, dto.DurationDays);
        decimal cost = (worker.MobilityAllowanceRate * days) + 200m;
        decimal preventedPenalty = 15_000m;

        // Cancel any pending Proposed orders for this worker so they cannot be double-approved later
        var pendingProposedOrders = await _dbContext.WorkerTransferOrders
            .Where(o => o.WorkerId == worker.Id && o.Status == "Proposed")
            .ToListAsync();
        foreach (var pending in pendingProposedOrders)
        {
            pending.Status = "Cancelled";
        }

        var order = new WorkerTransferOrder
        {
            WorkerId = worker.Id,
            FromPlantId = fromPlant.Id,
            ToPlantId = toPlant.Id,
            Reason = string.IsNullOrWhiteSpace(dto.Reason)
                ? $"{toPlant.Name} {worker.RoleOrSkill} hattı kapasite dengeleme görevlendirmesi"
                : dto.Reason,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(days),
            CreatedAt = DateTime.UtcNow,
            EstimatedCost = cost,
            PreventedPenaltyAmount = preventedPenalty,
            Status = "InTransit"
        };

        worker.Status = "InTransit";
        worker.CurrentPlantId = toPlant.Id;

        fromPlant.CurrentWorkerCount = Math.Max(0, fromPlant.CurrentWorkerCount - 1);
        fromPlant.ActiveCapacityPercent = Math.Round(Math.Min(115.0, fromPlant.ActiveCapacityPercent + 5.5), 1);
        toPlant.CurrentWorkerCount++;
        toPlant.ActiveCapacityPercent = Math.Round(Math.Max(60.0, toPlant.ActiveCapacityPercent - 6.5), 1);

        var openSeverities = await _dbContext.ExternalTelemetryAlerts
            .Where(a => !a.IsResolved && a.TargetPlantId == toPlant.Id)
            .Select(a => a.Severity)
            .ToListAsync();
        toPlant.Status = openSeverities.Any(s => string.Equals(s, "Critical", StringComparison.OrdinalIgnoreCase))
            ? "Critical"
            : (openSeverities.Count > 0 ? "Warning" : "Normal");

        _dbContext.WorkerTransferOrders.Add(order);

        var kpi = await _dbContext.OperationalKpis.FirstOrDefaultAsync();
        if (kpi != null)
        {
            var allPlants = await _dbContext.Plants.ToListAsync();
            if (allPlants.Count > 0)
            {
                kpi.MultiPlantWorkloadVariance = Math.Round(
                    allPlants.Max(p => p.ActiveCapacityPercent) - allPlants.Min(p => p.ActiveCapacityPercent), 1);
            }
            kpi.MobileWorkersDeployed++;
            kpi.TotalAvoidedPenaltyEur += preventedPenalty;
            kpi.TotalEmergencyLogisticsCostEur += cost;
            kpi.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        var hydratedOrder = await _dbContext.WorkerTransferOrders
            .Include(o => o.Worker)
            .Include(o => o.FromPlant)
            .Include(o => o.ToPlant)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        await _hubContext.Clients.All.SendAsync("ReceiveActionApproved", new
        {
            ActionType = "WorkerTransfer",
            OrderId = order.Id,
            Status = "InTransit",
            Message = $"✅ Personel Görevlendirme Tamamlandı: {worker.FullName} ({worker.RoleOrSkill}) {fromPlant.City} ➔ {toPlant.City} tesisine sevk edildi!"
        });

        var state = await _decisionEngine.GetDashboardStateAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", state);

        return Ok(new { success = true, order = hydratedOrder ?? order });
    }

    [HttpPost("recall/{orderId}")]
    public async Task<IActionResult> RecallWorkerToHomePlant(int orderId)
    {
        var order = await _dbContext.WorkerTransferOrders
            .Include(o => o.Worker)
            .Include(o => o.FromPlant)
            .Include(o => o.ToPlant)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.Worker == null)
            return NotFound(new { error = "Görevlendirme kaydı bulunamadı." });

        if (order.Status == "Completed")
            return Ok(new { success = true, order });

        if (order.Status != "InTransit")
            return BadRequest(new { error = "Yalnızca aktif görevde olan personel ana fabrikasına geri döndürülebilir." });

        var currentPlant = await _dbContext.Plants.FindAsync(order.Worker.CurrentPlantId);
        var homePlant = await _dbContext.Plants.FindAsync(order.Worker.HomePlantId);

        if (currentPlant != null && homePlant != null && currentPlant.Id != homePlant.Id)
        {
            currentPlant.CurrentWorkerCount = Math.Max(0, currentPlant.CurrentWorkerCount - 1);
            currentPlant.ActiveCapacityPercent = Math.Round(Math.Min(115.0, currentPlant.ActiveCapacityPercent + 6.5), 1);

            homePlant.CurrentWorkerCount++;
            homePlant.ActiveCapacityPercent = Math.Round(Math.Max(60.0, homePlant.ActiveCapacityPercent - 5.5), 1);
        }

        order.Status = "Completed";
        order.EndDate = DateTime.UtcNow;
        order.Worker.Status = "Active";
        order.Worker.CurrentPlantId = order.Worker.HomePlantId;

        var kpi = await _dbContext.OperationalKpis.FirstOrDefaultAsync();
        if (kpi != null)
        {
            var allPlants = await _dbContext.Plants.ToListAsync();
            if (allPlants.Count > 0)
            {
                kpi.MultiPlantWorkloadVariance = Math.Round(
                    allPlants.Max(p => p.ActiveCapacityPercent) - allPlants.Min(p => p.ActiveCapacityPercent), 1);
            }
            kpi.MobileWorkersDeployed = Math.Max(0, kpi.MobileWorkersDeployed - 1);
            kpi.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        var state = await _decisionEngine.GetDashboardStateAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", state);

        return Ok(new { success = true, message = $"{order.Worker.FullName} ana fabrikasına döndü.", order });
    }
}

public class DirectWorkerDispatchDto
{
    public int WorkerId { get; set; }
    public int ToPlantId { get; set; }
    public int DurationDays { get; set; } = 7;
    public string? Reason { get; set; }
}
