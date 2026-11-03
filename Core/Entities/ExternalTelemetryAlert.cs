namespace ERPES.ControlTower.Core.Entities;

public class ExternalTelemetryAlert
{
    public int Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? TransportCode { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }

    public int? TargetPlantId { get; set; }
    public Plant? TargetPlant { get; set; }

    public string? MaterialCode { get; set; }
    public int DelayHours { get; set; }
    public string Severity { get; set; } = "High";

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionSuggestion { get; set; } = string.Empty;
    public decimal RiskPenaltyAmount { get; set; }

    public int? AssociatedMaterialTransferOrderId { get; set; }
    public int? AssociatedWorkerTransferOrderId { get; set; }

    public bool IsResolved { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
