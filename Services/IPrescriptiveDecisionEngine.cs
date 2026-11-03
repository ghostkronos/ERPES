using ERPES.ControlTower.Core.DTOs;
using ERPES.ControlTower.Core.Entities;

namespace ERPES.ControlTower.Services;

public interface IPrescriptiveDecisionEngine
{
    Task ProcessTelemetryAsync(TelemetryDto telemetry, CancellationToken cancellationToken = default);
    Task<MaterialTransferOrder?> ApproveMaterialTransferAsync(int orderId, string approvedBy);
    Task<WorkerTransferOrder?> ApproveWorkerTransferAsync(int orderId, string approvedBy);
    Task<DashboardStateDto> GetDashboardStateAsync();
    Task ResetStateAsync();
}
