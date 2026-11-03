namespace ERPES.ControlTower.Core.Entities;

public class OperationalKpi
{
    public int Id { get; set; }
    public double OtifPercent { get; set; } = 98.4;
    public double AverageTimeToSurviveDays { get; set; } = 2.0;
    public double MultiPlantWorkloadVariance { get; set; } = 44.5;
    public decimal TotalAvoidedPenaltyEur { get; set; } = 0;
    public decimal TotalEmergencyLogisticsCostEur { get; set; } = 0;
    public int ActiveCrisisCount { get; set; } = 0;
    public int MobileWorkersDeployed { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
