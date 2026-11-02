-- =============================================================================
-- ERPES OPERASYON KULESİ — SUPABASE (POSTGRESQL) TAM VERİTABANI ŞEMASI & SEED
-- Bu SQL betiğini Supabase Dashboard -> SQL Editor ekranına yapıştırıp "Run" 
-- diyerek tüm tabloları, indeksleri ve başlangıç verilerini oluşturabilirsiniz.
-- =============================================================================

-- 1. PLANTS (ÜRETİM TESİSLERİ)
CREATE TABLE IF NOT EXISTS "Plants" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(150) NOT NULL,
    "Code" VARCHAR(32) NOT NULL UNIQUE,
    "City" VARCHAR(100) NOT NULL,
    "Latitude" DOUBLE PRECISION NOT NULL,
    "Longitude" DOUBLE PRECISION NOT NULL,
    "ActiveCapacityPercent" DOUBLE PRECISION NOT NULL DEFAULT 80.0,
    "RequiredWorkerCount" INTEGER NOT NULL DEFAULT 30,
    "CurrentWorkerCount" INTEGER NOT NULL DEFAULT 30,
    "Status" VARCHAR(32) NOT NULL DEFAULT 'Normal'
);

-- 2. INVENTORY ITEMS (TAMPON STOKLAR)
CREATE TABLE IF NOT EXISTS "InventoryItems" (
    "Id" SERIAL PRIMARY KEY,
    "PlantId" INTEGER NOT NULL REFERENCES "Plants"("Id") ON DELETE CASCADE,
    "MaterialCode" VARCHAR(64) NOT NULL,
    "MaterialName" VARCHAR(150) NOT NULL,
    "Quantity" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "SafetyStockThreshold" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "DailyConsumptionRate" DOUBLE PRECISION NOT NULL DEFAULT 1,
    "Unit" VARCHAR(24) NOT NULL DEFAULT 'Adet'
);

-- 3. WORKERS (PERSONEL & MOBİLİTE HAVUZU)
CREATE TABLE IF NOT EXISTS "Workers" (
    "Id" SERIAL PRIMARY KEY,
    "FullName" VARCHAR(120) NOT NULL,
    "EmployeeCode" VARCHAR(32) NOT NULL UNIQUE,
    "HomePlantId" INTEGER NOT NULL REFERENCES "Plants"("Id") ON DELETE RESTRICT,
    "CurrentPlantId" INTEGER NOT NULL REFERENCES "Plants"("Id") ON DELETE RESTRICT,
    "RoleOrSkill" VARCHAR(100) NOT NULL,
    "SkillScore" INTEGER NOT NULL CHECK ("SkillScore" BETWEEN 0 AND 100),
    "IsMobilityAccepted" BOOLEAN NOT NULL DEFAULT FALSE,
    "MobilityAllowanceRate" NUMERIC(12, 2) NOT NULL DEFAULT 0,
    "Status" VARCHAR(32) NOT NULL DEFAULT 'Active'
);

-- 4. WORKER TRANSFER ORDERS (İŞÇİ YÖNLENDİRME EMİRLERİ)
CREATE TABLE IF NOT EXISTS "WorkerTransferOrders" (
    "Id" SERIAL PRIMARY KEY,
    "WorkerId" INTEGER NOT NULL REFERENCES "Workers"("Id") ON DELETE CASCADE,
    "FromPlantId" INTEGER NOT NULL REFERENCES "Plants"("Id") ON DELETE RESTRICT,
    "ToPlantId" INTEGER NOT NULL REFERENCES "Plants"("Id") ON DELETE RESTRICT,
    "Reason" TEXT NOT NULL,
    "StartDate" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "EndDate" TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '7 days'),
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "EstimatedCost" NUMERIC(12, 2) NOT NULL DEFAULT 0,
    "PreventedPenaltyAmount" NUMERIC(12, 2) NOT NULL DEFAULT 0,
    "Status" VARCHAR(32) NOT NULL DEFAULT 'Proposed'
);

-- 5. MATERIAL TRANSFER ORDERS (STOK REBALANCING EMİRLERİ)
CREATE TABLE IF NOT EXISTS "MaterialTransferOrders" (
    "Id" SERIAL PRIMARY KEY,
    "MaterialCode" VARCHAR(64) NOT NULL,
    "MaterialName" VARCHAR(150) NOT NULL,
    "Quantity" DOUBLE PRECISION NOT NULL,
    "Unit" VARCHAR(24) NOT NULL DEFAULT 'Adet',
    "FromPlantId" INTEGER NOT NULL REFERENCES "Plants"("Id") ON DELETE RESTRICT,
    "ToPlantId" INTEGER NOT NULL REFERENCES "Plants"("Id") ON DELETE RESTRICT,
    "TransportMode" VARCHAR(50) NOT NULL DEFAULT 'ExpressTruck',
    "EstimatedCost" NUMERIC(12, 2) NOT NULL DEFAULT 0,
    "PreventedPenaltyAmount" NUMERIC(12, 2) NOT NULL DEFAULT 0,
    "Status" VARCHAR(32) NOT NULL DEFAULT 'Proposed',
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 6. EXTERNAL TELEMETRY ALERTS (LOJİSTİK & ÜRETİM KRİZ UYARILARI)
CREATE TABLE IF NOT EXISTS "ExternalTelemetryAlerts" (
    "Id" SERIAL PRIMARY KEY,
    "SourceType" VARCHAR(64) NOT NULL,
    "EventType" VARCHAR(64) NOT NULL,
    "TransportCode" VARCHAR(64),
    "Origin" VARCHAR(120),
    "Destination" VARCHAR(120),
    "TargetPlantId" INTEGER REFERENCES "Plants"("Id") ON DELETE SET NULL,
    "MaterialCode" VARCHAR(64),
    "DelayHours" INTEGER NOT NULL DEFAULT 0,
    "Severity" VARCHAR(32) NOT NULL DEFAULT 'High',
    "Title" VARCHAR(200) NOT NULL,
    "Description" TEXT NOT NULL,
    "ActionSuggestion" TEXT NOT NULL,
    "RiskPenaltyAmount" NUMERIC(12, 2) NOT NULL DEFAULT 0,
    "AssociatedMaterialTransferOrderId" INTEGER,
    "AssociatedWorkerTransferOrderId" INTEGER,
    "IsResolved" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 7. OPERATIONAL KPIS (KULE PERFORMANS GÖSTERGELERİ)
CREATE TABLE IF NOT EXISTS "OperationalKpis" (
    "Id" SERIAL PRIMARY KEY,
    "OtifPercent" DOUBLE PRECISION NOT NULL DEFAULT 98.4,
    "AverageTimeToSurviveDays" DOUBLE PRECISION NOT NULL DEFAULT 2.0,
    "MultiPlantWorkloadVariance" DOUBLE PRECISION NOT NULL DEFAULT 44.5,
    "TotalAvoidedPenaltyEur" NUMERIC(14, 2) NOT NULL DEFAULT 0,
    "TotalEmergencyLogisticsCostEur" NUMERIC(14, 2) NOT NULL DEFAULT 0,
    "ActiveCrisisCount" INTEGER NOT NULL DEFAULT 0,
    "MobileWorkersDeployed" INTEGER NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- İNDEKSLER (LINQ Performans Optimizasyonu)
CREATE INDEX IF NOT EXISTS "IX_Workers_Mobility_Skill" ON "Workers" ("IsMobilityAccepted", "Status", "RoleOrSkill", "MobilityAllowanceRate", "SkillScore" DESC);
CREATE INDEX IF NOT EXISTS "IX_Inventory_Material_Plant" ON "InventoryItems" ("MaterialCode", "PlantId");
