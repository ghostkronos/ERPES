using System.Threading.Channels;
using ERPES.ControlTower.Core.DTOs;

namespace ERPES.ControlTower.Infrastructure.Channels;

public class IngestionChannel
{
    private readonly Channel<TelemetryDto> _channel;

    public IngestionChannel(int capacity = 5000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<TelemetryDto>(options);
    }

    public ChannelWriter<TelemetryDto> Writer => _channel.Writer;
    public ChannelReader<TelemetryDto> Reader => _channel.Reader;

    /// <summary>
    /// Synchronizes background telemetry processing with baseline resets.
    /// </summary>
    public SemaphoreSlim ProcessingLock { get; } = new(1, 1);

    /// <summary>
    /// Drains any pending items from the channel (used during baseline reset).
    /// </summary>
    public int DrainPending()
    {
        int count = 0;
        while (_channel.Reader.TryRead(out _))
        {
            count++;
        }
        return count;
    }
}
