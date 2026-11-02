using System.Threading.Channels;
using ERPES.ControlTower.Core.DTOs;
using ERPES.ControlTower.Services;

namespace ERPES.ControlTower.Infrastructure.Channels;

public class BackgroundQueueService : BackgroundService
{
    private readonly ChannelReader<TelemetryDto> _channelReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundQueueService> _logger;

    public BackgroundQueueService(
        ChannelReader<TelemetryDto> channelReader,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundQueueService> logger)
    {
        _channelReader = channelReader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundQueueService initialized. Listening on ChannelReader<TelemetryDto>...");

        try
        {
            await foreach (var telemetry in _channelReader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var decisionEngine = scope.ServiceProvider.GetRequiredService<IPrescriptiveDecisionEngine>();
                    await decisionEngine.ProcessTelemetryAsync(telemetry, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing telemetry item from IngestionChannel.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BackgroundQueueService stopping gracefully.");
        }
    }
}
