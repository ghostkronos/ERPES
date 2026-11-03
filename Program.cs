using System.Text.Json.Serialization;
using ERPES.ControlTower.Hubs;
using ERPES.ControlTower.Infrastructure.Channels;
using ERPES.ControlTower.Infrastructure.Data;
using ERPES.ControlTower.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers & JSON Serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 2. EF Core Code-First DbContext (PostgreSQL / Supabase compatible + SQLite zero-config fallback)
var supabaseConn = builder.Configuration.GetConnectionString("SupabaseConnection");
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=controltower.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(supabaseConn) && supabaseConn.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(supabaseConn);
    }
    else
    {
        options.UseSqlite(defaultConn);
    }
});

// 3. System.Threading.Channels Decoupled Ingestion Pipeline
var ingestionChannel = new IngestionChannel(capacity: 5000);
builder.Services.AddSingleton(ingestionChannel);
builder.Services.AddSingleton(ingestionChannel.Writer);
builder.Services.AddSingleton(ingestionChannel.Reader);

// 4. SignalR Real-Time Broadcast Engine
builder.Services.AddSignalR();

// 5. Domain Services & Background Queue Consumer
builder.Services.AddHttpClient<LogisticsRadarService>();
builder.Services.AddSingleton<LogisticsRadarService>();
builder.Services.AddScoped<IPrescriptiveDecisionEngine, PrescriptiveDecisionEngine>();
builder.Services.AddHostedService<BackgroundQueueService>();

// 6. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Auto-initialize & seed database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbInitializer.Initialize(dbContext);
}

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapHub<TelemetryHub>("/hubs/control-tower");

app.Run();
