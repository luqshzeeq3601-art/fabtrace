# FabTrace – Live Demo Script & Interview Talking Points

Designed for demonstrating FabTrace during technical interviews with semiconductor manufacturers, OSATs, and machine builders (e.g. Intel, TF AMD, Inari, ViTrox, Pentamaster).

---

## 30-Second Elevator Pitch

> *"FabTrace is an end-to-end smart factory platform for semiconductor assembly and test floors. It provides high-speed, idempotent result ingestion from floor equipment, renders a real-time digital twin using SignalR, calculates factory OEE using T-SQL stored procedures, and detects subtle equipment degradation before failure using ML.NET time-series change-point detection—all built in native C# on .NET 10 with zero cloud dependencies."*

---

## Live Demonstration Flow (10-Minute Walkthrough)

### 1. Setup & Ingestion Replay (2 Minutes)
1. **Launch Services**:
   ```powershell
   dotnet run --project src/FabTrace.Api
   dotnet run --project src/FabTrace.Web
   ```
2. **Replay Plant History**:
   ```powershell
   dotnet run --project src/FabTrace.Generator -- --Weeks 4 --Seed 42
   ```
3. **Talking Point**:
   * *"Notice the generator talks to the API via HTTP with machine API keys, not direct database writes. Even if we replay the simulation twice, row counts remain identical because of client-generated GUIDs and SQL unique constraints."*

---

### 2. Live SCADA Floor View (2 Minutes)
1. Navigate to `http://localhost:5176`.
2. Point out the **Header & KPIs**:
   * Factory Local Time (UTC+8 Penang/Kulim time).
   * Live connection status indicator (`Live Connected`).
   * Machine status cards (Running, Idle, Down, Setup, Maintenance).
3. **Talking Point**:
   * *"SignalR streams state changes and alarm events directly from the ingestion pipeline over WebSockets. The Blazor server UI updates instantly with zero browser polling."*

---

### 3. Traceability Lookup (1 Minute)
1. Switch user to `engineer1` and click **Unit & Lot Traceability**.
2. Enter serial number `LOT-0001-001`.
3. Show the complete lifecycle:
   * Process steps traversed (`WIRE_BOND`, `FINAL_TEST`, `VISION_INSPECT`).
   * Machine codes, recipe versions, operator badges, cycle times, and analog readings (`PullStrength`, `ContactResistance`).
4. **Talking Point**:
   * *"If a customer returns a failed unit from the field, this lookup instantly connects the RMA serial number to the exact machine, recipe, and operator who touched it."*

---

### 4. Planted Problem 1: Defective Recipe Version (2 Minutes)
1. Open **Root-Cause Analysis** tab.
2. Click **Run Root-Cause Diagnosis**.
3. Point out the detection alert:
   * `Recipe 'BOND-QFN32' v2 shows an abnormal yield drop to ~85% compared to v1 (98%).`
   * Defect Pareto shows `WIRE_LIFT` as the primary defect.
4. **Talking Point**:
   * *"Between days 15 and 18, an engineer deployed recipe version 2 with lower ultrasonic power. Both wire bonders experienced immediate yield collapse on QFN32 packages. The root-cause scan isolates recipe versions without needing manual log collation."*

---

### 5. Planted Problem 2: ML.NET Predictive Maintenance (2 Minutes)
1. Click the **🔮 Predictive Maintenance** tab.
2. View the **Fleet Machine Health Status** cards.
   * `FT-02` shows a **Critical / Degrading** status with an amber/red warning.
3. Select `FT-02` and parameter `ContactResistance`.
4. Click **Execute ML Analysis**:
   * Show baseline mean (35 mΩ) vs drifting mean (> 45 mΩ).
   * Point out the **Structural Change Point** flag identified around day 20.
   * Highlight the recommendation: *"Test socket wear suspected. Immediately replace socket to prevent catastrophic line stoppage."*
5. **Talking Point**:
   * *"Traditional factories only react when an alarm trips (day 26). Here, ML.NET's `DetectIidChangePoint` caught the socket degradation 6 days earlier using Martingale scoring, completely within C#—no Python microservices required."*

---

### 6. AI Root-Cause Assistant (1 Minute)
1. Click **🤖 AI Assistant** tab.
2. Select suggested prompt: *"Analyze why Wire Bonder yield dropped on day 16 and identify the root cause."*
3. Show the answer and expand the **Data Transparency & Evidence** accordion.
4. **Talking Point**:
   * *"Notice that the local LLM never writes or executes arbitrary SQL. It calls parameterised C# tools (`GetYieldReport`, `PredictiveMaintenanceScan`) and grounds its answer entirely in verifiable evidence."*

---

## Interview Questions & Architectural Defenses

#### Q1: Why use real SQL Server instead of SQLite or EF Core In-Memory in tests?
> *"Semiconductor traceability requires enterprise SQL features: compiled stored procedures for analytics, windowed `LEAD()` functions for downtime calculations, and database unique constraints for idempotency. In-memory providers do not evaluate SQL constraints or transaction locks. Testing against SQL Server guarantees production fidelity."*

#### Q2: Why use client-generated GUIDs for idempotency?
> *"In a factory, network drops are common. If a machine controller sends a batch of 500 units and the network drops before receiving the HTTP 200, it must re-transmit. By assigning unique `ResultId` GUIDs at the machine level, SQL Server enforces idempotency at the database engine level. Replays are completely safe."*

#### Q3: Why write stored procedures instead of doing everything in EF Core LINQ?
> *"Aggregating millions of test readings and calculating state transitions with `LEAD()` inside C# memory would exhaust server RAM and saturate network bandwidth. Stored procedures allow the SQL Server query optimizer to execute rollups directly on disk indexes, returning only the compact summary."*

#### Q4: Why ML.NET instead of a Python Flask/FastAPI service?
> *"In industrial manufacturing, deploying Python runtimes on factory edge servers introduces version conflicts, virtual environment management overhead, and IPC latency. ML.NET runs in-process inside the .NET runtime, providing high-throughput native execution and single-binary deployment."*
