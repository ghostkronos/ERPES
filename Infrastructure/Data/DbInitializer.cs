using ERPES.ControlTower.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERPES.ControlTower.Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Plants.Any())
        {
            return;
        }

        SeedBaselineData(context);
    }

    private static void SeedBaselineData(ApplicationDbContext context)
    {

        var plantA = new Plant
        {
            Id = 1,
            Code = "PLANT-A",
            Name = "Tesis A — Kocaeli Otomotiv Montaj",
            City = "Kocaeli (Gebze)",
            Latitude = 40.8028,
            Longitude = 29.4307,
            ActiveCapacityPercent = 108.5,
            RequiredWorkerCount = 12,
            CurrentWorkerCount = 12,
            Status = "Warning"
        };

        var plantB = new Plant
        {
            Id = 2,
            Code = "PLANT-B",
            Name = "Tesis B — Bursa Güç Aktarma & Bileşen",
            City = "Bursa (Nilüfer)",
            Latitude = 40.2110,
            Longitude = 28.9320,
            ActiveCapacityPercent = 64.0,
            RequiredWorkerCount = 9,
            CurrentWorkerCount = 13, // +4 surplus workers ready for mobility
            Status = "Normal"
        };

        var plantC = new Plant
        {
            Id = 3,
            Code = "PLANT-C",
            Name = "Tesis C — İzmir Sensör & Elektronik",
            City = "İzmir (Çiğli)",
            Latitude = 38.5020,
            Longitude = 27.0250,
            ActiveCapacityPercent = 76.0,
            RequiredWorkerCount = 7,
            CurrentWorkerCount = 10, // +3 surplus workers ready for mobility
            Status = "Normal"
        };

        context.Plants.AddRange(plantA, plantB, plantC);
        context.SaveChanges();

        var inventory = new List<InventoryItem>
        {
            new InventoryItem
            {
                PlantId = plantA.Id,
                MaterialCode = "CHIP-MCU-AUTO",
                MaterialName = "Otomotiv Mikrodenetleyici Çip (32-Bit)",
                Quantity = 400,
                SafetyStockThreshold = 300,
                DailyConsumptionRate = 200,
                Unit = "Adet"
            },
            new InventoryItem
            {
                PlantId = plantA.Id,
                MaterialCode = "STEEL-COIL-S2",
                MaterialName = "Yüksek Dayanımlı Rulo Sac (Gövde)",
                Quantity = 2800,
                SafetyStockThreshold = 1000,
                DailyConsumptionRate = 300,
                Unit = "Kg"
            },
            new InventoryItem
            {
                PlantId = plantA.Id,
                MaterialCode = "SENSOR-LIDAR-F2",
                MaterialName = "ADAS Ön LiDAR & Radar Kiti",
                Quantity = 180,
                SafetyStockThreshold = 200,
                DailyConsumptionRate = 90,
                Unit = "Adet"
            },
            new InventoryItem
            {
                PlantId = plantB.Id,
                MaterialCode = "CHIP-MCU-AUTO",
                MaterialName = "Otomotiv Mikrodenetleyici Çip (32-Bit)",
                Quantity = 1850,
                SafetyStockThreshold = 400,
                DailyConsumptionRate = 80,
                Unit = "Adet"
            },
            new InventoryItem
            {
                PlantId = plantB.Id,
                MaterialCode = "GEAR-BOX-EV",
                MaterialName = "Elektrikli Araç Şanzıman Modülü",
                Quantity = 540,
                SafetyStockThreshold = 150,
                DailyConsumptionRate = 40,
                Unit = "Adet"
            },
            new InventoryItem
            {
                PlantId = plantB.Id,
                MaterialCode = "STEEL-COIL-S2",
                MaterialName = "Yüksek Dayanımlı Rulo Sac (Gövde)",
                Quantity = 4200,
                SafetyStockThreshold = 900,
                DailyConsumptionRate = 180,
                Unit = "Kg"
            },
            new InventoryItem
            {
                PlantId = plantC.Id,
                MaterialCode = "CHIP-MCU-AUTO",
                MaterialName = "Otomotiv Mikrodenetleyici Çip (32-Bit)",
                Quantity = 920,
                SafetyStockThreshold = 250,
                DailyConsumptionRate = 45,
                Unit = "Adet"
            },
            new InventoryItem
            {
                PlantId = plantC.Id,
                MaterialCode = "SENSOR-LIDAR-F2",
                MaterialName = "ADAS Ön LiDAR & Radar Kiti",
                Quantity = 1150,
                SafetyStockThreshold = 300,
                DailyConsumptionRate = 75,
                Unit = "Adet"
            }
        };

        context.InventoryItems.AddRange(inventory);
        context.SaveChanges();

        var workers = new List<Worker>
        {
            // =====================================================
            // PLANT A (KOCAELİ) — 12 KAYITLI UZMAN (CurrentWorkerCount = 12)
            // =====================================================
            new Worker { FullName = "Kemal Aksoy", EmployeeCode = "EMP-KOC-201", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Montaj Başteknisyeni", SkillScore = 98, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Deniz Aydın", EmployeeCode = "EMP-KOC-202", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Robotik Kaynak Teknisyeni", SkillScore = 92, IsMobilityAccepted = true, MobilityAllowanceRate = 115, Status = "Active" },
            new Worker { FullName = "Murat Çelik", EmployeeCode = "EMP-KOC-203", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 90, IsMobilityAccepted = true, MobilityAllowanceRate = 110, Status = "Active" },
            new Worker { FullName = "Ayşe Güneş", EmployeeCode = "EMP-KOC-204", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Kalite Güvence Uzmanı", SkillScore = 94, IsMobilityAccepted = true, MobilityAllowanceRate = 120, Status = "Active" },
            new Worker { FullName = "Fatih Özdemir", EmployeeCode = "EMP-KOC-205", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "PLC & Otomasyon Uzmanı", SkillScore = 95, IsMobilityAccepted = true, MobilityAllowanceRate = 130, Status = "Active" },
            new Worker { FullName = "Seda Korkmaz", EmployeeCode = "EMP-KOC-206", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Elektronik SMD Teknisyeni", SkillScore = 89, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Kaan Yavuz", EmployeeCode = "EMP-KOC-207", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Robotik Kaynak Teknisyeni", SkillScore = 88, IsMobilityAccepted = true, MobilityAllowanceRate = 105, Status = "Active" },
            new Worker { FullName = "Büşra Erdoğan", EmployeeCode = "EMP-KOC-208", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Kalite Güvence Uzmanı", SkillScore = 91, IsMobilityAccepted = true, MobilityAllowanceRate = 115, Status = "Active" },
            new Worker { FullName = "Ömer Faruk Şen", EmployeeCode = "EMP-KOC-209", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 87, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Tuğba Yalçın", EmployeeCode = "EMP-KOC-210", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Lojistik & Depo Operatörü", SkillScore = 93, IsMobilityAccepted = true, MobilityAllowanceRate = 100, Status = "Active" },
            new Worker { FullName = "Cihan Kurt", EmployeeCode = "EMP-KOC-211", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "Robotik Kaynak Teknisyeni", SkillScore = 86, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Volkan Taş", EmployeeCode = "EMP-KOC-212", HomePlantId = plantA.Id, CurrentPlantId = plantA.Id, RoleOrSkill = "PLC & Otomasyon Uzmanı", SkillScore = 92, IsMobilityAccepted = true, MobilityAllowanceRate = 125, Status = "Active" },

            // =====================================================
            // PLANT B (BURSA) — 13 KAYITLI UZMAN (CurrentWorkerCount = 13, Required = 9)
            // =====================================================
            new Worker { FullName = "Ahmet Yılmaz", EmployeeCode = "EMP-BUR-101", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 96, IsMobilityAccepted = true, MobilityAllowanceRate = 110, Status = "Active" },
            new Worker { FullName = "Mehmet Kaya", EmployeeCode = "EMP-BUR-102", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "Robotik Kaynak Teknisyeni", SkillScore = 93, IsMobilityAccepted = true, MobilityAllowanceRate = 105, Status = "Active" },
            new Worker { FullName = "Burak Demir", EmployeeCode = "EMP-BUR-103", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 91, IsMobilityAccepted = true, MobilityAllowanceRate = 95, Status = "Active" },
            new Worker { FullName = "Ali Öztürk", EmployeeCode = "EMP-BUR-104", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 89, IsMobilityAccepted = true, MobilityAllowanceRate = 90, Status = "Active" },
            new Worker { FullName = "Canan Yıldız", EmployeeCode = "EMP-BUR-105", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "Kalite Güvence Uzmanı", SkillScore = 97, IsMobilityAccepted = true, MobilityAllowanceRate = 125, Status = "Active" },
            new Worker { FullName = "Serkan Polat", EmployeeCode = "EMP-BUR-106", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "PLC & Otomasyon Uzmanı", SkillScore = 95, IsMobilityAccepted = true, MobilityAllowanceRate = 130, Status = "Active" },
            new Worker { FullName = "Emre Çetin", EmployeeCode = "EMP-BUR-107", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 91, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Derya Aktaş", EmployeeCode = "EMP-BUR-108", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "Elektronik SMD Teknisyeni", SkillScore = 92, IsMobilityAccepted = true, MobilityAllowanceRate = 115, Status = "Active" },
            new Worker { FullName = "Sinan Bozkurt", EmployeeCode = "EMP-BUR-109", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "Robotik Kaynak Teknisyeni", SkillScore = 90, IsMobilityAccepted = true, MobilityAllowanceRate = 105, Status = "Active" },
            new Worker { FullName = "Pınar Kaplan", EmployeeCode = "EMP-BUR-110", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "Kalite Güvence Uzmanı", SkillScore = 88, IsMobilityAccepted = true, MobilityAllowanceRate = 110, Status = "Active" },
            new Worker { FullName = "Yusuf Uysal", EmployeeCode = "EMP-BUR-111", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "PLC & Otomasyon Uzmanı", SkillScore = 93, IsMobilityAccepted = true, MobilityAllowanceRate = 120, Status = "Active" },
            new Worker { FullName = "Merve Sarı", EmployeeCode = "EMP-BUR-112", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "Lojistik & Depo Operatörü", SkillScore = 87, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Oğuzhan Tekin", EmployeeCode = "EMP-BUR-113", HomePlantId = plantB.Id, CurrentPlantId = plantB.Id, RoleOrSkill = "Robotik Kaynak Teknisyeni", SkillScore = 89, IsMobilityAccepted = true, MobilityAllowanceRate = 100, Status = "Active" },

            // =====================================================
            // PLANT C (İZMİR) — 10 KAYITLI UZMAN (CurrentWorkerCount = 10, Required = 7)
            // =====================================================
            new Worker { FullName = "Selin Koç", EmployeeCode = "EMP-IZM-301", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "Elektronik SMD Teknisyeni", SkillScore = 96, IsMobilityAccepted = true, MobilityAllowanceRate = 120, Status = "Active" },
            new Worker { FullName = "Gökhan Arslan", EmployeeCode = "EMP-IZM-302", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 92, IsMobilityAccepted = true, MobilityAllowanceRate = 105, Status = "Active" },
            new Worker { FullName = "Zeynep Şahin", EmployeeCode = "EMP-IZM-303", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "Kalite Güvence Uzmanı", SkillScore = 94, IsMobilityAccepted = true, MobilityAllowanceRate = 115, Status = "Active" },
            new Worker { FullName = "Hakan Doğan", EmployeeCode = "EMP-IZM-304", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "Elektronik SMD Teknisyeni", SkillScore = 85, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Tolga Güler", EmployeeCode = "EMP-IZM-305", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "PLC & Otomasyon Uzmanı", SkillScore = 95, IsMobilityAccepted = true, MobilityAllowanceRate = 125, Status = "Active" },
            new Worker { FullName = "Ebru Karaca", EmployeeCode = "EMP-IZM-306", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "Robotik Kaynak Teknisyeni", SkillScore = 91, IsMobilityAccepted = true, MobilityAllowanceRate = 110, Status = "Active" },
            new Worker { FullName = "Barış Tunç", EmployeeCode = "EMP-IZM-307", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "CNC Operatörü", SkillScore = 89, IsMobilityAccepted = true, MobilityAllowanceRate = 100, Status = "Active" },
            new Worker { FullName = "Gamze Avcı", EmployeeCode = "EMP-IZM-308", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "Elektronik SMD Teknisyeni", SkillScore = 93, IsMobilityAccepted = true, MobilityAllowanceRate = 115, Status = "Active" },
            new Worker { FullName = "Uğur Akın", EmployeeCode = "EMP-IZM-309", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "Kalite Güvence Uzmanı", SkillScore = 88, IsMobilityAccepted = false, MobilityAllowanceRate = 0, Status = "Active" },
            new Worker { FullName = "Nilüfer Sezer", EmployeeCode = "EMP-IZM-310", HomePlantId = plantC.Id, CurrentPlantId = plantC.Id, RoleOrSkill = "PLC & Otomasyon Uzmanı", SkillScore = 90, IsMobilityAccepted = true, MobilityAllowanceRate = 120, Status = "Active" }
        };

        context.Workers.AddRange(workers);

        var kpi = new OperationalKpi
        {
            OtifPercent = 98.4,
            AverageTimeToSurviveDays = 2.0,
            MultiPlantWorkloadVariance = 44.5,
            TotalAvoidedPenaltyEur = 0,
            TotalEmergencyLogisticsCostEur = 0,
            ActiveCrisisCount = 0,
            MobileWorkersDeployed = 0,
            UpdatedAt = DateTime.UtcNow
        };

        context.OperationalKpis.Add(kpi);
        context.SaveChanges();

        // Advance PostgreSQL SERIAL sequence past explicit Plant IDs (1, 2, 3)
        if (context.Database.IsNpgsql())
        {
            context.Database.ExecuteSqlRaw(
                "SELECT setval(pg_get_serial_sequence('\"Plants\"', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM \"Plants\"), 1));");
        }
    }

    public static void ResetToBaseline(ApplicationDbContext context)
    {
        using var transaction = context.Database.BeginTransaction();

        // Delete child/dependent tables first before parent Plants
        context.ExternalTelemetryAlerts.ExecuteDelete();
        context.WorkerTransferOrders.ExecuteDelete();
        context.MaterialTransferOrders.ExecuteDelete();
        context.OperationalKpis.ExecuteDelete();
        context.Workers.ExecuteDelete();
        context.InventoryItems.ExecuteDelete();
        context.Plants.ExecuteDelete();
        context.ChangeTracker.Clear();

        SeedBaselineData(context);
        transaction.Commit();
    }
}
