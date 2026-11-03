using System.Text.Json.Serialization;

namespace ERPES.ControlTower.Core.DTOs;

public class IngestResultDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Queued";

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
