using Microsoft.AspNetCore.SignalR;

namespace ERPES.ControlTower.Hubs;

public class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("TelemetryHub client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("ConnectedConfirmation", new
        {
            ConnectionId = Context.ConnectionId,
            ServerTime = DateTime.UtcNow,
            Message = "ERPES Operasyon Kulesi Gerçek Zamanlı Telemetri Akışına Bağlandı."
        });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("TelemetryHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
