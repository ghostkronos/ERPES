namespace ERPES.ControlTower.Core.Entities;

public class InventoryItem
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public Plant? Plant { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double SafetyStockThreshold { get; set; }
    public double DailyConsumptionRate { get; set; }
    public string Unit { get; set; } = "Adet";

    public double TimeToSurviveDays => DailyConsumptionRate > 0 ? Math.Round(Quantity / DailyConsumptionRate, 1) : 999.0;
}
