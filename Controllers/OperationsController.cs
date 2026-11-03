using System.Threading.Channels;
using ERPES.ControlTower.Core.DTOs;
using ERPES.ControlTower.Services;
using Microsoft.AspNetCore.Mvc;

namespace ERPES.ControlTower.Controllers;

[ApiController]
[Route("api/v1/operations")]
public class OperationsController : ControllerBase
{
    private readonly IPrescriptiveDecisionEngine _decisionEngine;
    private readonly LogisticsRadarService _radarService;
    private readonly ChannelWriter<TelemetryDto> _channel;
    private readonly ILogger<OperationsController> _logger;

    public OperationsController(
        IPrescriptiveDecisionEngine decisionEngine,
        LogisticsRadarService radarService,
        ChannelWriter<TelemetryDto> channel,
        ILogger<OperationsController> logger)
    {
        _decisionEngine = decisionEngine;
        _radarService = radarService;
        _channel = channel;
        _logger = logger;
    }

    [HttpGet("state")]
    public async Task<ActionResult<DashboardStateDto>> GetState()
    {
        var state = await _decisionEngine.GetDashboardStateAsync();
        return Ok(state);
    }

    [HttpGet("radar")]
    public async Task<ActionResult<LogisticsRadarDto>> GetLiveRadar()
    {
        var radar = await _radarService.GetLiveRadarSnapshotAsync();
        return Ok(radar);
    }

    [HttpPost("approve-action")]
    public async Task<IActionResult> ApproveAction([FromBody] ActionApprovalDto dto)
    {
        _logger.LogInformation("Prescriptive action approved: Type={Type}, OrderId={OrderId}, Approver={Approver}",
            dto.ActionType, dto.OrderId, dto.ApprovedBy);

        if (dto.ActionType == "MaterialTransfer" && dto.OrderId.HasValue)
        {
            var result = await _decisionEngine.ApproveMaterialTransferAsync(dto.OrderId.Value, dto.ApprovedBy);
            return Ok(new { success = true, order = result });
        }
        else if (dto.ActionType == "WorkerTransfer" && dto.OrderId.HasValue)
        {
            var result = await _decisionEngine.ApproveWorkerTransferAsync(dto.OrderId.Value, dto.ApprovedBy);
            return Ok(new { success = true, order = result });
        }

        return BadRequest(new { error = "Geçersiz işlem tipi veya eksik emir numarası." });
    }

    [HttpPost("simulate/rotterdam-crisis")]
    public async Task<IActionResult> SimulateRotterdamCrisis()
    {
        var variants = new[]
        {
            new TelemetryDto
            {
                SourceType = "MarineLogistics",
                EventType = "PortStrikeDelay",
                TransportCode = "MSC-ROTTERDAM-991",
                MaterialCode = "CHIP-MCU-AUTO",
                TargetPlantId = 1, // Kocaeli
                DelayHours = 144,
                Severity = "Critical",
                Description = "Rotterdam liman işçileri grevi nedeniyle MSC Rotterdam-991 konteyner gemisi demirledi. Kocaeli tesisi için 1.200 adet mikroçip sevkiyatı 6 gün gecikti."
            },
            new TelemetryDto
            {
                SourceType = "MarineLogistics",
                EventType = "PortCongestionDelay",
                TransportCode = "CMA-CGM-BOSPHORUS",
                MaterialCode = "STEEL-COIL-S2",
                TargetPlantId = 1, // Kocaeli
                DelayHours = 96,
                Severity = "Critical",
                Description = "Pire Limanı ve Çanakkale Boğazı yoğunluğu nedeniyle CMA CGM Bosphorus gemisi 96 saat gecikti. Kocaeli gövde hattında rulo sac stoku kritik seviyeye indi."
            },
            new TelemetryDto
            {
                SourceType = "MarineLogistics",
                EventType = "CustomsClearanceHold",
                TransportCode = "ARKAS-MARMARA-EXP",
                MaterialCode = "CHIP-MCU-AUTO",
                TargetPlantId = 2, // Bursa
                DelayHours = 72,
                Severity = "High",
                Description = "Gemlik Limanı gümrük ve fırtına kısıtlaması nedeniyle Arkas Marmara Express gemisi açıkta bekliyor. Bursa güç aktarma tesisi için acil mikroçip takviyesi gerekiyor."
            }
        };

        var chosen = variants[Random.Shared.Next(variants.Length)];
        await _channel.WriteAsync(chosen);
        return Accepted(new { message = "Deniz lojistik krizi senaryosu başlatıldı.", scenarioTitle = chosen.Description, telemetry = chosen });
    }

    [HttpPost("simulate/air-cargo-delay")]
    public async Task<IActionResult> SimulateAirCargoDelay()
    {
        var variants = new[]
        {
            new TelemetryDto
            {
                SourceType = "AirCargo",
                EventType = "FlightDelayed",
                TransportCode = "TK-CARGO-704",
                MaterialCode = "SENSOR-LIDAR-F2",
                TargetPlantId = 1, // Kocaeli (Donor: İzmir)
                DelayHours = 36,
                Severity = "High",
                Description = "Frankfurt (FRA) çıkışlı TK-CARGO-704 kargo uçağı hava muhalefeti nedeniyle 36 saat rötarlı. Kocaeli otonom sürüş hattı için İzmir tesisinden acil LiDAR kiti kaydırılmalı."
            },
            new TelemetryDto
            {
                SourceType = "AirCargo",
                EventType = "SlotRestrictionDelay",
                TransportCode = "DHL-EURO-412",
                MaterialCode = "CHIP-MCU-AUTO",
                TargetPlantId = 3, // İzmir (Donor: Bursa)
                DelayHours = 48,
                Severity = "Critical",
                Description = "Münih Kargo Hub slot kısıtlaması nedeniyle yarı iletken kargo uçağı 48 saat gecikti. İzmir elektronik dizgi hattı için Bursa deposundan ekspres mikroçip sevkiyatı öneriliyor."
            },
            new TelemetryDto
            {
                SourceType = "AirCargo",
                EventType = "TechnicalGroundStop",
                TransportCode = "MNG-CARGO-308",
                MaterialCode = "SENSOR-LIDAR-F2",
                TargetPlantId = 1, // Kocaeli (Donor: İzmir)
                DelayHours = 42,
                Severity = "High",
                Description = "Amsterdam Schiphol kargo terminalinde teknik bakım nedeniyle uçuş 42 saat ertelendi. İzmir emniyet stoğundan Kocaeli tesisine ekspres panelvan çıkarılması gerekiyor."
            }
        };

        var chosen = variants[Random.Shared.Next(variants.Length)];
        await _channel.WriteAsync(chosen);
        return Accepted(new { message = "Hava kargo rötar senaryosu başlatıldı.", scenarioTitle = chosen.Description, telemetry = chosen });
    }

    [HttpPost("simulate/workforce-bottleneck")]
    public async Task<IActionResult> SimulateWorkforceBottleneck()
    {
        var variants = new[]
        {
            new TelemetryDto
            {
                SourceType = "Workforce",
                EventType = "WorkerAbsenteeism_CNC",
                TransportCode = "CNC Operatörü",
                TargetPlantId = 1, // Kocaeli
                Value = 3,
                Severity = "High",
                Description = "Kocaeli (Tesis A) CNC işleme hattında vardiya devamsızlığı ve sipariş artışı nedeniyle 3 uzman CNC Operatörü açığı oluştu."
            },
            new TelemetryDto
            {
                SourceType = "Workforce",
                EventType = "RoboticWeldingSurge",
                TransportCode = "Robotik Kaynak Teknisyeni",
                TargetPlantId = 1, // Kocaeli
                Value = 2,
                Severity = "Critical",
                Description = "Kocaeli (Tesis A) şasi kaynak hattında yeni ihracat siparişi nedeniyle hat yükü %122'ye çıktı. Diğer fabrikalardan 2 Robotik Kaynak Teknisyeni takviyesi gerekiyor."
            },
            new TelemetryDto
            {
                SourceType = "Workforce",
                EventType = "QualityAuditBottleneck",
                TransportCode = "Kalite Güvence Uzmanı",
                TargetPlantId = 3, // İzmir
                Value = 2,
                Severity = "High",
                Description = "İzmir (Tesis C) ADAS sensör kalibrasyon ve son kontrol hattında 2 Kalite Güvence Uzmanı açığı tespit edildi."
            },
            new TelemetryDto
            {
                SourceType = "Workforce",
                EventType = "PlcAutomationFault",
                TransportCode = "PLC & Otomasyon Uzmanı",
                TargetPlantId = 2, // Bursa
                Value = 2,
                Severity = "High",
                Description = "Bursa (Tesis B) elektrikli şanzıman montaj hattında Robot/PLC senkronizasyon revizyonu için 2 PLC & Otomasyon Uzmanı desteği talep edildi."
            }
        };

        var chosen = variants[Random.Shared.Next(variants.Length)];
        await _channel.WriteAsync(chosen);
        return Accepted(new { message = "Personel darboğazı senaryosu başlatıldı.", scenarioTitle = chosen.Description, telemetry = chosen });
    }

    [HttpPost("simulate/random-crisis")]
    public async Task<IActionResult> SimulateRandomCrisis()
    {
        int roll = Random.Shared.Next(3);
        if (roll == 0) return await SimulateRotterdamCrisis();
        if (roll == 1) return await SimulateAirCargoDelay();
        return await SimulateWorkforceBottleneck();
    }

    [HttpPost("reset")]
    public async Task<IActionResult> ResetSystem()
    {
        await _decisionEngine.ResetStateAsync();
        return Ok(new { message = "Tüm fabrikalar, stoklar ve personel kadroları başlangıç durumuna sıfırlandı." });
    }
}
