using ERPES.ControlTower.Core.Entities;

namespace ERPES.ControlTower.Core.DTOs;

public class DashboardStateDto
{
    public OperationalKpi Kpis { get; set; } = new();
    public List<Plant> Plants { get; set; } = new();
    public List<ExternalTelemetryAlert> ActiveAlerts { get; set; } = new();
    public List<MaterialTransferOrder> MaterialTransfers { get; set; } = new();
    public List<WorkerTransferOrder> WorkerTransfers { get; set; } = new();
    public List<Worker> MobileWorkers { get; set; } = new();
}
