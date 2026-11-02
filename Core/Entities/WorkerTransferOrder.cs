namespace ERPES.ControlTower.Core.Entities;

public class WorkerTransferOrder
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public Worker? Worker { get; set; }

    public int FromPlantId { get; set; }
    public Plant? FromPlant { get; set; }

    public int ToPlantId { get; set; }
    public Plant? ToPlant { get; set; }

    public string Reason { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal EstimatedCost { get; set; }
    public decimal PreventedPenaltyAmount { get; set; }
    public string Status { get; set; } = "Proposed";
}
