using System.Text.Json.Serialization;

namespace ERPES.ControlTower.Core.DTOs;

public class TelemetryDto
{
    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("transportCode")]
    public string? TransportCode { get; set; }

    [JsonPropertyName("materialCode")]
    public string? MaterialCode { get; set; }

    [JsonPropertyName("targetPlantId")]
    public int? TargetPlantId { get; set; }

    [JsonPropertyName("delayHours")]
    public int DelayHours { get; set; }

    [JsonPropertyName("value")]
    public double? Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "High";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
