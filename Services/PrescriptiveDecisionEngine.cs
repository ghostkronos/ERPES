using ERPES.ControlTower.Core.DTOs;
using ERPES.ControlTower.Core.Entities;
using ERPES.ControlTower.Hubs;
using ERPES.ControlTower.Infrastructure.Channels;
using ERPES.ControlTower.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ERPES.ControlTower.Services;

public class PrescriptiveDecisionEngine : IPrescriptiveDecisionEngine
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly IngestionChannel _ingestionChannel;
    private readonly ILogger<PrescriptiveDecisionEngine> _logger;

    public PrescriptiveDecisionEngine(
        ApplicationDbContext dbContext,
        IHubContext<TelemetryHub> hubContext,
        IngestionChannel ingestionChannel,
        ILogger<PrescriptiveDecisionEngine> logger)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _ingestionChannel = ingestionChannel;
        _logger = logger;
    }

    public async Task ProcessTelemetryAsync(TelemetryDto telemetry, CancellationToken cancellationToken = default)
    {
        var eventType = telemetry.EventType ?? string.Empty;
        _logger.LogInformation("Processing incoming telemetry: Source={Source}, Event={Event}, Code={Code}",
            telemetry.SourceType, eventType, telemetry.TransportCode ?? telemetry.MaterialCode);

        if (telemetry.SourceType == "MarineLogistics" || eventType.Contains("Port") || eventType.Contains("Customs"))
        {
            await HandleLogisticsDelayAsync(telemetry, cancellationToken);
        }
        else if (telemetry.SourceType == "AirCargo" || eventType.Contains("Flight") || eventType.Contains("Slot") || eventType.Contains("Technical"))
        {
            await HandleAirCargoDelayAsync(telemetry, cancellationToken);
        }
        else if (telemetry.SourceType == "Workforce" || eventType.Contains("Worker") || eventType.Contains("Welding") || eventType.Contains("Quality") || eventType.Contains("Plc"))
        {
            await HandleWorkforceBottleneckAsync(telemetry, cancellationToken);
        }
        else
        {
            await HandleGenericTelemetryAsync(telemetry, cancellationToken);
        }

        var updatedState = await GetDashboardStateAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", updatedState, cancellationToken);
    }

    private async Task<Plant?> ResolveTargetPlantAsync(int? requestedId, CancellationToken cancellationToken)
    {
        if (requestedId.HasValue)
        {
            return await _dbContext.Plants
                .Include(p => p.InventoryItems)
                .FirstOrDefaultAsync(p => p.Id == requestedId.Value, cancellationToken);
        }

        return await _dbContext.Plants
            .Include(p => p.InventoryItems)
            .FirstOrDefaultAsync(p => p.Code == "PLANT-A", cancellationToken);
    }

    private async Task RecomputePlantStatusAsync(Plant? plant, CancellationToken cancellationToken = default)
    {
        if (plant == null) return;

        var openSeverities = await _dbContext.ExternalTelemetryAlerts
            .Where(a => !a.IsResolved && a.TargetPlantId == plant.Id)
            .Select(a => a.Severity)
            .ToListAsync(cancellationToken);

        if (openSeverities.Any(s => string.Equals(s, "Critical", StringComparison.OrdinalIgnoreCase)))
        {
            plant.Status = "Critical";
        }
        else if (openSeverities.Count > 0)
        {
            plant.Status = "Warning";
        }
        else
        {
            plant.Status = "Normal";
        }
    }

    private async Task RecalculateOperationalKpisAsync(OperationalKpi kpi, CancellationToken cancellationToken = default)
    {
        var allPlants = await _dbContext.Plants.ToListAsync(cancellationToken);
        if (allPlants.Count > 0)
        {
            double maxCap = allPlants.Max(p => p.ActiveCapacityPercent);
            double minCap = allPlants.Min(p => p.ActiveCapacityPercent);
            kpi.MultiPlantWorkloadVariance = Math.Round(maxCap - minCap, 1);
        }

        var allInventory = await _dbContext.InventoryItems.ToListAsync(cancellationToken);
        if (allInventory.Count > 0)
        {
            kpi.AverageTimeToSurviveDays = Math.Round(allInventory.Min(i => i.TimeToSurviveDays), 1);
        }

        kpi.ActiveCrisisCount = await _dbContext.ExternalTelemetryAlerts.CountAsync(a => !a.IsResolved, cancellationToken);
        kpi.UpdatedAt = DateTime.UtcNow;
    }

    private async Task HandleLogisticsDelayAsync(TelemetryDto telemetry, CancellationToken cancellationToken)
    {
        var targetPlant = await ResolveTargetPlantAsync(telemetry.TargetPlantId, cancellationToken);

        if (targetPlant == null) return;

        var materialCode = telemetry.MaterialCode ?? "CHIP-MCU-AUTO";
        var targetInventory = targetPlant.InventoryItems.FirstOrDefault(i => i.MaterialCode == materialCode)
            ?? targetPlant.InventoryItems.FirstOrDefault();

        if (targetInventory == null) return;

        double ttsDays = targetInventory.TimeToSurviveDays;
        decimal slaPenalty = Random.Shared.Next(65, 98) * 1000m;
        int deficitUnits = materialCode == "STEEL-COIL-S2" ? 1200 : 800;

        var donorInventory = await _dbContext.InventoryItems
            .Include(i => i.Plant)
            .Where(i => i.MaterialCode == targetInventory.MaterialCode && i.PlantId != targetPlant.Id)
            .OrderByDescending(i => (i.Quantity - i.SafetyStockThreshold))
            .FirstOrDefaultAsync(cancellationToken);

        int fromPlantId = donorInventory?.PlantId ?? (targetPlant.Id == 2 ? 3 : 2);
        var donorPlant = await _dbContext.Plants.FindAsync(new object[] { fromPlantId }, cancellationToken);
        string fromPlantName = donorPlant?.Name ?? "Tesis B — Bursa Güç Aktarma";
        decimal estimatedFreightCost = Random.Shared.Next(32, 54) * 100m;

        var transferOrder = new MaterialTransferOrder
        {
            MaterialCode = targetInventory.MaterialCode,
            MaterialName = targetInventory.MaterialName,
            Quantity = deficitUnits,
            Unit = targetInventory.Unit,
            FromPlantId = fromPlantId,
            ToPlantId = targetPlant.Id,
            TransportMode = "ExpressTruck",
            EstimatedCost = estimatedFreightCost,
            PreventedPenaltyAmount = slaPenalty,
            Status = "Proposed",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.MaterialTransferOrders.Add(transferOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var alert = new ExternalTelemetryAlert
        {
            SourceType = telemetry.SourceType,
            EventType = telemetry.EventType,
            TransportCode = telemetry.TransportCode ?? "MSC-ROTTERDAM-991",
            Origin = "Uluslararası Deniz Hattı",
            Destination = targetPlant.Name,
            TargetPlantId = targetPlant.Id,
            MaterialCode = targetInventory.MaterialCode,
            DelayHours = telemetry.DelayHours > 0 ? telemetry.DelayHours : 120,
            Severity = "Critical",
            Title = $"🚢 DENİZ LOJİSTİK AKSAMASI: {telemetry.TransportCode} (+{telemetry.DelayHours} Saat Gecikme)",
            Description = telemetry.Description ?? $"{targetPlant.Name} tesisine gelen deniz sevkiyatı gecikti. Stok yetme süresi {ttsDays:F1} güne düştü.",
            ActionSuggestion = $"{fromPlantName} emniyet stoğundan {deficitUnits:N0} {targetInventory.Unit} {targetInventory.MaterialName} ekspres tır ile sevk edilmesi önerildi. Maliyet: €{estimatedFreightCost:N0} | Önlenen Ceza: €{slaPenalty:N0}.",
            RiskPenaltyAmount = slaPenalty,
            AssociatedMaterialTransferOrderId = transferOrder.Id,
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        };

        targetPlant.Status = "Critical";
        _dbContext.ExternalTelemetryAlerts.Add(alert);

        var kpi = await _dbContext.OperationalKpis.FirstOrDefaultAsync(cancellationToken);
        if (kpi != null)
        {
            kpi.ActiveCrisisCount = await _dbContext.ExternalTelemetryAlerts.CountAsync(a => !a.IsResolved, cancellationToken) + 1;
            kpi.AverageTimeToSurviveDays = ttsDays;
            kpi.OtifPercent = 91.5;
            kpi.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync("ReceiveTelemetryAlert", alert, cancellationToken);
    }

    private async Task HandleAirCargoDelayAsync(TelemetryDto telemetry, CancellationToken cancellationToken)
    {
        var targetPlant = await ResolveTargetPlantAsync(telemetry.TargetPlantId, cancellationToken);

        if (targetPlant == null) return;

        string materialCode = telemetry.MaterialCode ?? "SENSOR-LIDAR-F2";

        // Pick donor plant that has this material (usually Plant C İzmir for LiDAR, or Plant B Bursa for Chips)
        var donorInventory = await _dbContext.InventoryItems
            .Include(i => i.Plant)
            .Where(i => i.MaterialCode == materialCode && i.PlantId != targetPlant.Id)
            .OrderByDescending(i => i.Quantity)
            .FirstOrDefaultAsync(cancellationToken);

        var donorPlant = donorInventory?.Plant
            ?? await _dbContext.Plants
                .Where(p => p.Id != targetPlant.Id)
                .OrderByDescending(p => p.Code == "PLANT-C")
                .FirstOrDefaultAsync(cancellationToken);
        if (donorPlant == null) return;

        string materialName = donorInventory?.MaterialName ?? "ADAS Ön LiDAR & Radar Kiti";
        decimal slaPenalty = Random.Shared.Next(48, 72) * 1000m;
        decimal expressCost = Random.Shared.Next(24, 38) * 100m;
        int transferQty = materialCode == "SENSOR-LIDAR-F2" ? 250 : 350;

        var transferOrder = new MaterialTransferOrder
        {
            MaterialCode = materialCode,
            MaterialName = materialName,
            Quantity = transferQty,
            Unit = "Adet",
            FromPlantId = donorPlant.Id,
            ToPlantId = targetPlant.Id,
            TransportMode = "ExpressVan",
            EstimatedCost = expressCost,
            PreventedPenaltyAmount = slaPenalty,
            Status = "Proposed",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.MaterialTransferOrders.Add(transferOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var alert = new ExternalTelemetryAlert
        {
            SourceType = "AirCargo",
            EventType = telemetry.EventType,
            TransportCode = telemetry.TransportCode ?? "TK-CARGO-704",
            Origin = "Avrupa Hava Kargo Hub",
            Destination = targetPlant.Name,
            TargetPlantId = targetPlant.Id,
            MaterialCode = materialCode,
            DelayHours = telemetry.DelayHours > 0 ? telemetry.DelayHours : 36,
            Severity = "High",
            Title = $"✈️ HAVA KARGO RÖTARI: {telemetry.TransportCode} (+{telemetry.DelayHours} Saat)",
            Description = telemetry.Description ?? $"Hava kargo sevkiyatında +{telemetry.DelayHours} saat rötar oluştu.",
            ActionSuggestion = $"{donorPlant.Name} ({donorPlant.City}) emniyet stoğundan {transferQty} adet {materialName} ürününün ekspres araçla {targetPlant.City} tesisine sevk edilmesi önerildi (Maliyet: €{expressCost:N0}, Önlenen Ceza: €{slaPenalty:N0}).",
            RiskPenaltyAmount = slaPenalty,
            AssociatedMaterialTransferOrderId = transferOrder.Id,
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        };

        targetPlant.Status = "Warning";
        _dbContext.ExternalTelemetryAlerts.Add(alert);

        var kpi = await _dbContext.OperationalKpis.FirstOrDefaultAsync(cancellationToken);
        if (kpi != null)
        {
            kpi.ActiveCrisisCount = await _dbContext.ExternalTelemetryAlerts.CountAsync(a => !a.IsResolved, cancellationToken) + 1;
            kpi.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetryAlert", alert, cancellationToken);
    }

    private async Task HandleWorkforceBottleneckAsync(TelemetryDto telemetry, CancellationToken cancellationToken)
    {
        var targetPlant = await ResolveTargetPlantAsync(telemetry.TargetPlantId, cancellationToken);

        if (targetPlant == null) return;

        string requiredSkill = !string.IsNullOrWhiteSpace(telemetry.TransportCode)
            ? telemetry.TransportCode
            : "CNC Operatörü";

        int neededCount = telemetry.Value.HasValue ? Math.Max(1, (int)telemetry.Value.Value) : 2;
        decimal slaRisk = Random.Shared.Next(38, 64) * 1000m;

        // Exclude workers already proposed in an open WorkerTransferOrder
        var pendingProposedWorkerIds = await _dbContext.WorkerTransferOrders
            .Where(o => o.Status == "Proposed")
            .Select(o => o.WorkerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Strict Rule: IsMobilityAccepted == true, Status == "Active", and donor plant has surplus
        var primaryPool = await _dbContext.Workers
            .Include(w => w.CurrentPlant)
            .Where(w => w.CurrentPlantId != targetPlant.Id)
            .Where(w => !pendingProposedWorkerIds.Contains(w.Id))
            .Where(w => w.IsMobilityAccepted == true && w.Status == "Active")
            .Where(w => w.CurrentPlant != null && w.CurrentPlant.CurrentWorkerCount > w.CurrentPlant.RequiredWorkerCount)
            .Where(w => w.RoleOrSkill.Contains(requiredSkill))
            .OrderBy(w => w.MobilityAllowanceRate)
            .ThenByDescending(w => w.SkillScore)
            .ToListAsync(cancellationToken);

        var candidateWorkers = SelectRespectingDonorSurplus(primaryPool, neededCount);

        // Fallback if specific role exhausted: still strictly enforce IsMobilityAccepted, Active, and donor surplus
        if (!candidateWorkers.Any())
        {
            var fallbackPool = await _dbContext.Workers
                .Include(w => w.CurrentPlant)
                .Where(w => w.CurrentPlantId != targetPlant.Id)
                .Where(w => !pendingProposedWorkerIds.Contains(w.Id))
                .Where(w => w.IsMobilityAccepted == true && w.Status == "Active")
                .Where(w => w.CurrentPlant != null && w.CurrentPlant.CurrentWorkerCount > w.CurrentPlant.RequiredWorkerCount)
                .OrderBy(w => w.MobilityAllowanceRate)
                .ThenByDescending(w => w.SkillScore)
                .ToListAsync(cancellationToken);

            candidateWorkers = SelectRespectingDonorSurplus(fallbackPool, neededCount);
        }

        if (!candidateWorkers.Any()) return;

        decimal totalTransferCost = candidateWorkers.Sum(w => (w.MobilityAllowanceRate * 7) + 200m);

        var transferOrders = new List<WorkerTransferOrder>();
        foreach (var worker in candidateWorkers)
        {
            var order = new WorkerTransferOrder
            {
                WorkerId = worker.Id,
                FromPlantId = worker.CurrentPlantId,
                ToPlantId = targetPlant.Id,
                Reason = $"{targetPlant.City} {requiredSkill} hattı kapasite dengeleme için 7 günlük geçici görevlendirme.",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(7),
                EstimatedCost = (worker.MobilityAllowanceRate * 7) + 200m,
                PreventedPenaltyAmount = Math.Round(slaRisk / candidateWorkers.Count, 0),
                Status = "Proposed",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.WorkerTransferOrders.Add(order);
            transferOrders.Add(order);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        string workerNames = string.Join(", ", candidateWorkers.Select(w => $"{w.FullName} ({w.CurrentPlant?.City})"));
        var alert = new ExternalTelemetryAlert
        {
            SourceType = "Workforce",
            EventType = telemetry.EventType,
            TargetPlantId = targetPlant.Id,
            Severity = "High",
            Title = $"👷 PERSONEL DARBOĞAZI: {targetPlant.City} — {candidateWorkers.Count} {requiredSkill} Talebi",
            Description = telemetry.Description ?? $"{targetPlant.Name} üretim hattında iş yükü artışı nedeniyle uzman personel ihtiyacı doğdu.",
            ActionSuggestion = $"Rotasyona uygun ({workerNames}) personellerinin {targetPlant.City} tesisine geçici görevlendirilmesi önerildi. Maliyet: €{totalTransferCost:N0} | Önlenen Ceza: €{slaRisk:N0}.",
            RiskPenaltyAmount = slaRisk,
            AssociatedWorkerTransferOrderId = transferOrders.FirstOrDefault()?.Id,
            IsResolved = false,
            CreatedAt = DateTime.UtcNow
        };

        targetPlant.Status = "Warning";
        targetPlant.ActiveCapacityPercent = Math.Round(Math.Min(124.0, targetPlant.ActiveCapacityPercent + 11.5), 1);
        _dbContext.ExternalTelemetryAlerts.Add(alert);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var kpi = await _dbContext.OperationalKpis.FirstOrDefaultAsync(cancellationToken);
        if (kpi != null)
        {
            await RecalculateOperationalKpisAsync(kpi, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _hubContext.Clients.All.SendAsync("ReceiveTelemetryAlert", alert, cancellationToken);
    }

    private async Task HandleGenericTelemetryAsync(TelemetryDto telemetry, CancellationToken cancellationToken)
    {
        var alert = new ExternalTelemetryAlert
        {
            SourceType = telemetry.SourceType,
            EventType = telemetry.EventType,
            TargetPlantId = telemetry.TargetPlantId,
            Severity = telemetry.Severity,
            Title = $"📡 Operasyon Bildirimi: {telemetry.EventType}",
            Description = telemetry.Description ?? "Saha verisi işlendi.",
            ActionSuggestion = "Operasyon parametreleri dengede.",
            IsResolved = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ExternalTelemetryAlerts.Add(alert);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _hubContext.Clients.All.SendAsync("ReceiveTelemetryAlert", alert, cancellationToken);
    }

    private static List<Worker> SelectRespectingDonorSurplus(List<Worker> orderedCandidates, int neededCount)
    {
        var selected = new List<Worker>();
        var remainingSurplusByPlant = new Dictionary<int, int>();

        foreach (var worker in orderedCandidates)
        {
            if (selected.Count >= neededCount) break;
            if (worker.CurrentPlant == null) continue;

            if (!remainingSurplusByPlant.TryGetValue(worker.CurrentPlantId, out int surplus))
            {
                surplus = worker.CurrentPlant.CurrentWorkerCount - worker.CurrentPlant.RequiredWorkerCount;
            }

            if (surplus <= 0) continue;

            remainingSurplusByPlant[worker.CurrentPlantId] = surplus - 1;
            selected.Add(worker);
        }

        return selected;
    }

    public async Task<MaterialTransferOrder?> ApproveMaterialTransferAsync(int orderId, string approvedBy)
    {
        var order = await _dbContext.MaterialTransferOrders
            .Include(o => o.FromPlant)
            .Include(o => o.ToPlant)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.Status == "InTransit" || order.Status == "Delivered")
            return order;

        order.Status = "InTransit";

        var donorStock = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.PlantId == order.FromPlantId && i.MaterialCode == order.MaterialCode);

        var targetStock = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.PlantId == order.ToPlantId && i.MaterialCode == order.MaterialCode);

        // Transfer only available donor stock so phantom inventory is never created
        double actualQty = donorStock != null
            ? Math.Min(donorStock.Quantity, order.Quantity)
            : order.Quantity;
        order.Quantity = actualQty;

        if (donorStock != null)
        {
            donorStock.Quantity = Math.Max(0, donorStock.Quantity - actualQty);
        }

        if (targetStock != null)
        {
            targetStock.Quantity += actualQty;
        }

        var alerts = await _dbContext.ExternalTelemetryAlerts
            .Where(a => a.AssociatedMaterialTransferOrderId == order.Id ||
                        (!a.IsResolved && a.MaterialCode == order.MaterialCode && a.TargetPlantId == order.ToPlantId))
            .ToListAsync();

        foreach (var a in alerts)
        {
            a.IsResolved = true;
        }

        await _dbContext.SaveChangesAsync();

        var targetPlant = await _dbContext.Plants.FindAsync(order.ToPlantId);
        if (targetPlant != null)
        {
            targetPlant.ActiveCapacityPercent = Math.Round(Math.Max(65.0, targetPlant.ActiveCapacityPercent - 4.0), 1);
            await RecomputePlantStatusAsync(targetPlant);
        }

        var kpi = await _dbContext.OperationalKpis.FirstOrDefaultAsync();
        if (kpi != null)
        {
            kpi.TotalAvoidedPenaltyEur += order.PreventedPenaltyAmount;
            kpi.TotalEmergencyLogisticsCostEur += order.EstimatedCost;
            await RecalculateOperationalKpisAsync(kpi);
            kpi.OtifPercent = kpi.ActiveCrisisCount == 0 ? 99.1 : 95.4;
        }

        await _dbContext.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ReceiveActionApproved", new
        {
            ActionType = "MaterialTransfer",
            OrderId = order.Id,
            FromPlantId = order.FromPlantId,
            ToPlantId = order.ToPlantId,
            Status = "InTransit",
            Message = $"✅ Sevkiyat Emri Onaylandı: {order.FromPlant?.City} ➔ {order.ToPlant?.City} arası {order.Quantity:N0} {order.Unit} {order.MaterialName} yola çıktı!",
            ApprovedBy = approvedBy
        });

        var state = await GetDashboardStateAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", state);

        return order;
    }

    public async Task<WorkerTransferOrder?> ApproveWorkerTransferAsync(int orderId, string approvedBy)
    {
        var clickedOrder = await _dbContext.WorkerTransferOrders
            .Include(o => o.Worker)
            .Include(o => o.FromPlant)
            .Include(o => o.ToPlant)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (clickedOrder == null) return null;
        if (clickedOrder.Status != "Proposed") return clickedOrder;

        var proposedOrders = await _dbContext.WorkerTransferOrders
            .Include(o => o.Worker)
            .Include(o => o.FromPlant)
            .Include(o => o.ToPlant)
            .Where(o => o.Status == "Proposed" && o.ToPlantId == clickedOrder.ToPlantId)
            .ToListAsync();

        var primaryOrder = proposedOrders.FirstOrDefault(o => o.Id == orderId) ?? clickedOrder;

        decimal batchPenaltyAvoided = 0;
        decimal batchCost = 0;
        int transferredCount = 0;
        var workerNames = new List<string>();
        var activatedOrders = new List<WorkerTransferOrder>();

        foreach (var order in proposedOrders)
        {
            if (order.Worker == null ||
                !order.Worker.IsMobilityAccepted ||
                order.Worker.Status != "Active" ||
                order.Worker.CurrentPlantId != order.FromPlantId)
            {
                order.Status = "Cancelled";
                continue;
            }

            var donorPlant = await _dbContext.Plants.FindAsync(order.FromPlantId);
            var targetPlant = await _dbContext.Plants.FindAsync(order.ToPlantId);
            if (donorPlant == null || targetPlant == null || donorPlant.CurrentWorkerCount <= donorPlant.RequiredWorkerCount)
            {
                order.Status = "Cancelled";
                continue;
            }

            order.Status = "InTransit";
            batchPenaltyAvoided += order.PreventedPenaltyAmount;
            batchCost += order.EstimatedCost;
            transferredCount++;
            activatedOrders.Add(order);

            order.Worker.Status = "InTransit";
            order.Worker.CurrentPlantId = order.ToPlantId;
            workerNames.Add(order.Worker.FullName);

            donorPlant.CurrentWorkerCount = Math.Max(0, donorPlant.CurrentWorkerCount - 1);
            donorPlant.ActiveCapacityPercent = Math.Round(Math.Min(115.0, donorPlant.ActiveCapacityPercent + 5.5), 1);

            targetPlant.CurrentWorkerCount++;
            targetPlant.ActiveCapacityPercent = Math.Round(Math.Max(60.0, targetPlant.ActiveCapacityPercent - 6.5), 1);
        }

        var unresolvedWorkforceAlerts = await _dbContext.ExternalTelemetryAlerts
            .Where(a => !a.IsResolved && a.SourceType == "Workforce" && a.TargetPlantId == clickedOrder.ToPlantId)
            .ToListAsync();

        foreach (var a in unresolvedWorkforceAlerts)
        {
            a.IsResolved = true;
        }

        await _dbContext.SaveChangesAsync();

        var resolvedTargetPlant = await _dbContext.Plants.FindAsync(clickedOrder.ToPlantId);
        await RecomputePlantStatusAsync(resolvedTargetPlant);

        var kpi = await _dbContext.OperationalKpis.FirstOrDefaultAsync();
        if (kpi != null)
        {
            if (transferredCount > 0)
            {
                kpi.TotalAvoidedPenaltyEur += batchPenaltyAvoided;
                kpi.TotalEmergencyLogisticsCostEur += batchCost;
                kpi.MobileWorkersDeployed += transferredCount;
            }
            await RecalculateOperationalKpisAsync(kpi);
        }

        await _dbContext.SaveChangesAsync();

        if (transferredCount > 0)
        {
            string sourceCities = string.Join("/", activatedOrders.Select(o => o.FromPlant?.City ?? "Bursa").Distinct());
            string targetCity = primaryOrder.ToPlant?.City ?? "Kocaeli";

            await _hubContext.Clients.All.SendAsync("ReceiveActionApproved", new
            {
                ActionType = "WorkerTransfer",
                OrderId = primaryOrder.Id,
                Status = "InTransit",
                Message = $"✅ Personel Görevlendirme Onaylandı: {string.Join(", ", workerNames)} ({sourceCities} ➔ {targetCity}) hattında sevk edildi!",
                ApprovedBy = approvedBy
            });
        }

        var state = await GetDashboardStateAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", state);

        return primaryOrder;
    }

    public async Task<DashboardStateDto> GetDashboardStateAsync()
    {
        var kpis = await _dbContext.OperationalKpis.FirstOrDefaultAsync() ?? new OperationalKpi();
        var plants = await _dbContext.Plants
            .Include(p => p.InventoryItems)
            .Include(p => p.CurrentWorkers)
            .ToListAsync();

        var alerts = await _dbContext.ExternalTelemetryAlerts
            .OrderByDescending(a => a.CreatedAt)
            .Take(15)
            .ToListAsync();

        var materialTransfers = await _dbContext.MaterialTransferOrders
            .Include(m => m.FromPlant)
            .Include(m => m.ToPlant)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync();

        var workerTransfers = await _dbContext.WorkerTransferOrders
            .Include(w => w.Worker)
            .Include(w => w.FromPlant)
            .Include(w => w.ToPlant)
            .OrderByDescending(w => w.CreatedAt)
            .Take(15)
            .ToListAsync();

        var allWorkers = await _dbContext.Workers
            .Include(w => w.CurrentPlant)
            .Include(w => w.HomePlant)
            .OrderByDescending(w => w.IsMobilityAccepted)
            .ThenByDescending(w => w.SkillScore)
            .ToListAsync();

        return new DashboardStateDto
        {
            Kpis = kpis,
            Plants = plants,
            ActiveAlerts = alerts,
            MaterialTransfers = materialTransfers,
            WorkerTransfers = workerTransfers,
            MobileWorkers = allWorkers
        };
    }

    public async Task ResetStateAsync()
    {
        await _ingestionChannel.ProcessingLock.WaitAsync();
        try
        {
            _ingestionChannel.DrainPending();
            DbInitializer.ResetToBaseline(_dbContext);
        }
        finally
        {
            _ingestionChannel.ProcessingLock.Release();
        }

        var state = await GetDashboardStateAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", state);
    }
}
