# ERPES CONTROL TOWER — MULTI-AGENT WORKSPACE CONFIGURATION (`AGENTS.md`)

## 1. Mission & Architectural Overview
ERPES Operasyon Kulesi is a real-time multi-plant manufacturing, supply chain, and workforce mobility control tower MVP.

### Core Technology Stack
- **Runtime & API**: ASP.NET Core Web API (C#)
- **Database & ORM**: Entity Framework Core Code-First — Dual-mode support for **PostgreSQL (Supabase compatible)** via `Npgsql.EntityFrameworkCore.PostgreSQL` and local zero-config **SQLite** fallback.
- **High-Throughput Async Queue**: `System.Threading.Channels` (`IngestionChannel` / `ChannelWriter<TelemetryDto>`)
- **Background Processing**: `BackgroundQueueService : BackgroundService` (`IHostedService`) with scoped `ApplicationDbContext` resolution.
- **Real-Time Telemetry Broadcast**: ASP.NET Core SignalR (`TelemetryHub`)
- **Frontend Dashboard**: `wwwroot` Cyber-Dark Command UI with Leaflet.js Geo-Radar (OpenSky ADS-B + Maritime AIS), KPI panels, and 1-click Prescriptive Action approval.

### Mandatory Contracts
1. **Core Ingest Endpoint**:
   - Route: `POST /api/v1/telemetry/ingest`
   - Behavior: Non-blocking write to `ChannelWriter<TelemetryDto>`, returns `HTTP 202 Accepted` (`IngestResultDto`).
2. **Domain Entities (`Core/Entities`)**:
   - `Plant`
   - `InventoryItem`
   - `Worker` (`bool IsMobilityAccepted`, `string RoleOrSkill`, `int SkillScore`, `decimal MobilityAllowanceRate`)
   - `WorkerTransferOrder`
   - `ExternalTelemetryAlert`
   - `MaterialTransferOrder` & `OperationalKpi`
3. **Workforce Mobility Rule**:
   - On workforce deficit at target plant, query workers across other plants where `IsMobilityAccepted == true`, `Status == "Active"`, matching `RoleOrSkill`, donor plant has surplus (`CurrentWorkerCount > RequiredWorkerCount`), ranked by minimal `MobilityAllowanceRate` then highest `SkillScore` via LINQ.

---

## 2. Multi-Agent Execution Protocol (Token-Saver Tiering)

### `[Architect]` — Advanced / Reasoning Tier (`pro` / `inherit`)
- **Scope**: System architecture, EF Core entity relationships, `System.Threading.Channels` thread-safety, scoped `ApplicationDbContext` lifecycle inside `BackgroundService`, SignalR hub contracts, and LINQ optimization algorithms.
- **Escalation Trigger**: Activated when sub-agents hit compilation/runtime errors lasting >2 iterations or concurrency/race condition issues.

### `[Backend-Dev]` — Lightweight / Fast Tier (`flash`)
- **Scope**: `Core/Entities`, `Core/DTOs`, `Controllers/TelemetryController`, `Infrastructure/Channels/IngestionChannel`, `Infrastructure/Channels/BackgroundQueueService`, LINQ filters, and seed generators.

### `[Frontend-Dev]` — Lightweight / Fast Tier (`flash`)
- **Scope**: `wwwroot` Web Dashboard, Leaflet.js plant coordinates, vessel/aircraft radar layers, transfer polylines, SignalR client listener, KPI panels, and crisis simulation triggers.

### `[QA-Reviewer]` — Lightweight / Fast Tier (`flash_lite` / `flash`)
- **Scope**: Code review against business constraints (verifying `IsMobilityAccepted == true` is strictly enforced, non-blocking `202 Accepted` on `/api/v1/telemetry/ingest`, and scoped DI safety).

---

## 3. Strict Language, Token Conservation & Human-in-the-Loop Rules
1. **USER-FACING INTERACTION (TURKISH ONLY)**:
   - All summaries, explanations, status updates, confirmation questions, and walkthroughs presented to the user **MUST BE IN TURKISH**.
2. **INTERNAL AGENT-TO-AGENT COMMUNICATION (CONCISE TECHNICAL ENGLISH)**:
   - All internal handoffs, planning logs, technical contracts, code comments, and sub-task delegations **MUST BE IN CONCISE, STRICT TECHNICAL ENGLISH**.
3. **HUMAN-IN-THE-LOOP & INFRASTRUCTURE ESCALATION**:
   - Never add database/infrastructure migration or credential input panels into the user-facing web dashboard.
   - Perform all Supabase/PostgreSQL operations directly via MCP tools (`supabase_mcp`) and backend configuration (`appsettings.json`).
   - Whenever an infrastructure action requires user approval (e.g., restoring an inactive/paused project, creating a new cloud project, or providing a database password), **STOP and ask the user directly for confirmation/input**.

---

## 4. Repository Structure
```text
ERPES.ControlTower/
├── AGENTS.md
├── Core/
│   ├── Entities/
│   │   ├── Plant.cs
│   │   ├── InventoryItem.cs
│   │   ├── Worker.cs
│   │   ├── WorkerTransferOrder.cs
│   │   ├── MaterialTransferOrder.cs
│   │   ├── ExternalTelemetryAlert.cs
│   │   └── OperationalKpi.cs
│   └── DTOs/
│       ├── TelemetryDto.cs
│       ├── IngestResultDto.cs
│       ├── WorkerTransferRequestDto.cs
│       ├── ActionApprovalDto.cs
│       ├── DashboardStateDto.cs
│       └── LogisticsRadarDto.cs
├── Infrastructure/
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   └── DbInitializer.cs
│   └── Channels/
│       ├── IngestionChannel.cs
│       └── BackgroundQueueService.cs
├── Services/
│   ├── IPrescriptiveDecisionEngine.cs
│   ├── PrescriptiveDecisionEngine.cs
│   └── LogisticsRadarService.cs
├── Hubs/
│   └── TelemetryHub.cs
├── Controllers/
│   ├── TelemetryController.cs
│   └── OperationsController.cs
└── wwwroot/
    ├── index.html
    ├── css/style.css
    └── js/app.js
```
