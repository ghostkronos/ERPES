namespace ERPES.ControlTower.Core.Entities;

public class MaterialTransferOrder
{
    public int Id { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Unit { get; set; } = "Adet";

    public int FromPlantId { get; set; }
    public Plant? FromPlant { get; set; }

    public int ToPlantId { get; set; }
    public Plant? ToPlant { get; set; }

    public string TransportMode { get; set; } = "ExpressTruck";
    public decimal EstimatedCost { get; set; }
    public decimal PreventedPenaltyAmount { get; set; }
    public string Status { get; set; } = "Proposed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
