# FabTrace – Test Plan & Verification Matrix

FabTrace uses **xUnit v3** on the **Microsoft Testing Platform** to run unit and integration tests against real SQL Server instances.

---

## Testing Principles

1. **Real Database, No In-Memory Substitutes**:
   * Semiconductor factories depend on stored procedures, window functions (`LEAD`), constraints, and exact locking semantics. In-memory or SQLite providers cannot test these.
   * Every integration test executes against real SQL Server Express (`FabTrace_Test`); CI uses a SQL Server 2022 container.
2. **State Isolation**:
   * All integration tests inherit from [`IntegrationTest`](../tests/FabTrace.Tests/Integration/IntegrationTest.cs).
   * Before each test executes, `InitializeAsync` resets and purges production tables (`Units`, `InspectionResults`, `TestReadings`, `Alarms`, `MachineStateEvents`), ensuring test independence.
3. **HTTP Pipeline Fidelity**:
   * Tests run through the complete ASP.NET Core pipeline using `WebApplicationFactory<Program>`, verifying authentication headers, route bindings, model validations, and status codes.

---

## Test Suite Summary

Total Test Count: **109 Passing Tests**

| Test Area | Scope | Test Classes | Count |
|---|---|---|---|
| **API Ingestion & Idempotency** | Integration | `InspectionResultIngestionTests`, `MachineEventIngestionTests`, `GeneratorReplayTests` | 23 |
| **Authentication & Roles** | Unit & Integration | `ApiKeyAuthTests`, `AdminApiKeyTests`, `ApiKeyHasherTests`, `UserAuthTests` | 15 |
| **SCADA & Live Floor** | Integration | `ScadaLiveTests` | 6 |
| **Traceability & Query** | Integration | `TraceQueryTests`, `SchemaTests` | 14 |
| **Stored Procedures & OEE** | Integration | `YieldReportTests`, `DowntimeReportTests`, `OeeReportTests`, `PerformanceIndexTests` | 10 |
| **Automated Root-Cause** | Integration | `RootCauseTests` | 2 |
| **AI Root-Cause Assistant** | Integration | `AiRootCauseAssistantTests` | 3 |
| **Predictive Maintenance (ML.NET)** | Unit & Integration | `PredictiveMaintenanceTests`, `PredictiveMaintenanceIntegrationTests` | 6 |
| **Core Pure Logic & Simulator** | Unit | `ShiftTests`, `GeneratorOptionsTests`, `FactorySimulatorTests` | 30 |

---

## Detailed Test Verification Matrix

### 1. Ingestion & Idempotency
* `Post_batch_with_duplicates_returns_duplicate_status`: Posting the same `ResultId` twice returns HTTP 200 with `Created` on pass 1 and `Duplicate` on pass 2; row count remains 1.
* `Concurrent_identical_batches_are_idempotent`: Parallel POST tasks with identical records trigger SQL Server unique index error 2601/2627, handled cleanly by clearing change tracker and reloading.
* `Generator_replay_leaves_row_counts_unchanged`: Replaying the deterministic 4-week simulation twice results in identical row counts.
* `Batch_with_invalid_recipe_or_step_is_rejected`: Rejects items referencing non-existent master data with descriptive error messages without failing valid items in the batch.

### 2. SCADA & Live Floor (SignalR)
* `Ingesting_state_event_broadcasts_to_SignalR_hub`: Machine state transition triggers live WebSocket push to connected clients.
* `Ingesting_alarm_event_broadcasts_live_alarm`: New alarm appears in real-time on the floor twin.
* `Clear_alarm_endpoint_broadcasts_cleared_event`: Alarm clear event notifies subscribers and clears active badge.

### 3. Traceability & Reporting Stored Procedures
* `Unit_trace_returns_full_history`: Querying `GET /api/units/{serialNumber}` returns lot number, machine code, recipe version, verdict, cycle time, and analog readings.
* `sp_GetMachineDowntime_calculates_state_durations`: Windowed `LEAD()` calculates exact seconds spent in `Running`, `Idle`, `Down`, `Setup`, and `Maintenance`.
* `sp_GetOeeReport_computes_availability_performance_quality`: Verifies mathematically sound OEE calculations clamped to 0–100%.
* `Reporting_indexes_accelerate_queries`: Validates index usage on `(InspectedAt, MachineId)` and `(RecipeId, Verdict)`.

### 4. Root-Cause Analysis
* `Root_cause_analysis_detects_recipe_yield_drop`: Identifies recipe `BOND-QFN32` v2 yield collapse to ~85% with `WIRE_LIFT` defect code.
* `Root_cause_analysis_detects_tester_contact_resistance_drift`: Detects statistical upward drift in `ContactResistance` on `FT-02`.

### 5. AI Root-Cause Assistant
* `Assistant_invokes_parameterized_csharp_tools`: Simulates tool calling via `IChatClient` and verifies read-only C# tools are executed with parameter validation.
* `Assistant_captures_evidence_transparency`: Ensures tool outputs are embedded in `AiRootCauseResponseDto.Evidence` for user auditability.

### 6. Predictive Maintenance (ML.NET Time-Series)
* `MlNet_detects_step_change_point_in_time_series`: Synthetic step change from 35.0 to 55.0 triggers change-point alert using Martingale scoring.
* `MlNet_detects_transient_spike_anomaly`: An outlier spike at index 15 is flagged as a spike anomaly.
* `Predictive_maintenance_detects_planted_socket_wear_change_point_on_FT02`: Replaying `FT-02` contact resistance values accurately flags the change point and outputs the `"socket wear"` warning.
* `Predictive_maintenance_reports_healthy_status_for_stable_machine`: Confirms normal variance on `WB-01` produces a `Healthy` status without false alarms.

---

## How to Execute Tests

```powershell
# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter-class "*PredictiveMaintenanceIntegrationTests*"

# Run only unit tests
dotnet test --filter-class "*PredictiveMaintenanceTests*"
```
