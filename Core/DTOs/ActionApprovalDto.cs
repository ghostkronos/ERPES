using System.Text.Json.Serialization;

namespace ERPES.ControlTower.Core.DTOs;

public class ActionApprovalDto
{
    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = string.Empty;

    [JsonPropertyName("alertId")]
    public int? AlertId { get; set; }

    [JsonPropertyName("orderId")]
    public int? OrderId { get; set; }

    [JsonPropertyName("approvedBy")]
    public string ApprovedBy { get; set; } = "Kule Operasyon Lideri";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
