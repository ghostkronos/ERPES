namespace ERPES.ControlTower.Core.Entities;

public class Plant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ActiveCapacityPercent { get; set; }
    public int RequiredWorkerCount { get; set; }
    public int CurrentWorkerCount { get; set; }
    public string Status { get; set; } = "Normal";

    public List<InventoryItem> InventoryItems { get; set; } = new();
    public List<Worker> CurrentWorkers { get; set; } = new();
}
