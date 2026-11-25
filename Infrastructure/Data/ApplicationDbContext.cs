using ERPES.ControlTower.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERPES.ControlTower.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<WorkerTransferOrder> WorkerTransferOrders => Set<WorkerTransferOrder>();
    public DbSet<MaterialTransferOrder> MaterialTransferOrders => Set<MaterialTransferOrder>();
    public DbSet<ExternalTelemetryAlert> ExternalTelemetryAlerts => Set<ExternalTelemetryAlert>();
    public DbSet<OperationalKpi> OperationalKpis => Set<OperationalKpi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<InventoryItem>()
            .HasOne(i => i.Plant)
            .WithMany(p => p.InventoryItems)
            .HasForeignKey(i => i.PlantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Worker>()
            .HasOne(w => w.HomePlant)
            .WithMany()
            .HasForeignKey(w => w.HomePlantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Worker>()
            .HasOne(w => w.CurrentPlant)
            .WithMany(p => p.CurrentWorkers)
            .HasForeignKey(w => w.CurrentPlantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkerTransferOrder>()
            .HasOne(o => o.Worker)
            .WithMany()
            .HasForeignKey(o => o.WorkerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkerTransferOrder>()
            .HasOne(o => o.FromPlant)
            .WithMany()
            .HasForeignKey(o => o.FromPlantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkerTransferOrder>()
            .HasOne(o => o.ToPlant)
            .WithMany()
            .HasForeignKey(o => o.ToPlantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaterialTransferOrder>()
            .HasOne(o => o.FromPlant)
            .WithMany()
            .HasForeignKey(o => o.FromPlantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MaterialTransferOrder>()
            .HasOne(o => o.ToPlant)
            .WithMany()
            .HasForeignKey(o => o.ToPlantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExternalTelemetryAlert>()
            .HasOne(a => a.TargetPlant)
            .WithMany()
            .HasForeignKey(a => a.TargetPlantId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mirror unique constraints from supabase_migration.sql
        modelBuilder.Entity<Plant>()
            .HasIndex(p => p.Code)
            .IsUnique();

        modelBuilder.Entity<Worker>()
            .HasIndex(w => w.EmployeeCode)
            .IsUnique();

        // SQLite cannot translate ORDER BY on decimal columns unless converted to double
        if (Database.IsSqlite())
        {
            modelBuilder.Entity<Worker>()
                .Property(w => w.MobilityAllowanceRate)
                .HasConversion<double>();

            modelBuilder.Entity<WorkerTransferOrder>()
                .Property(o => o.EstimatedCost)
                .HasConversion<double>();
            modelBuilder.Entity<WorkerTransferOrder>()
                .Property(o => o.PreventedPenaltyAmount)
                .HasConversion<double>();

            modelBuilder.Entity<MaterialTransferOrder>()
                .Property(o => o.EstimatedCost)
                .HasConversion<double>();
            modelBuilder.Entity<MaterialTransferOrder>()
                .Property(o => o.PreventedPenaltyAmount)
                .HasConversion<double>();

            modelBuilder.Entity<ExternalTelemetryAlert>()
                .Property(a => a.RiskPenaltyAmount)
                .HasConversion<double>();

            modelBuilder.Entity<OperationalKpi>()
                .Property(k => k.TotalAvoidedPenaltyEur)
                .HasConversion<double>();
            modelBuilder.Entity<OperationalKpi>()
                .Property(k => k.TotalEmergencyLogisticsCostEur)
                .HasConversion<double>();
        }
    }
}
