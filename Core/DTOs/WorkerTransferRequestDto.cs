using System.Text.Json.Serialization;

namespace ERPES.ControlTower.Core.DTOs;

public class WorkerTransferRequestDto
{
    [JsonPropertyName("targetPlantId")]
    public int TargetPlantId { get; set; }

    [JsonPropertyName("requiredRoleOrSkill")]
    public string RequiredRoleOrSkill { get; set; } = "CNC Operatörü";

    [JsonPropertyName("deficitCount")]
    public int DeficitCount { get; set; } = 3;

    [JsonPropertyName("durationDays")]
    public int DurationDays { get; set; } = 7;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "Acil hat dengeleme ve darboğaz çözümü";
}
