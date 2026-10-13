# FabTrace – Product Requirements

## Problem statement

On a semiconductor assembly and test floor, each machine knows what it did to each unit, but that knowledge stays on the machine. When yield drops, engineers lose hours collecting logs to answer simple questions: which lot, which machine, which recipe, which shift. Results are also lost when the network or server is down, and resent results are counted twice.

## Objectives

| Objective | Measure |
|---|---|
| Never lose or double-count a result | Replaying the same history twice leaves row counts unchanged (automated test) |
| Trace any unit | One lookup shows lot, machine, recipe, operator, verdict and readings |
| See the floor live | Machine state, active alarms and throughput update without refreshing |
| Find root causes quickly | The two planted problems (bad recipe, drifting machine) are identifiable from the root-cause view |
| Explainable in an interview | Every design decision is written down next to the code |

## Target audience

- **Operator**: sees machine status and alarms for the current shift.
- **Process / yield engineer**: traces units, compares machines, recipes and shifts, investigates data discrepancies.
- **Admin**: manages users, machines and API keys.
- **Interviewer**: reads the code and asks why.

## Tech stack

| Choice | Why |
|---|---|
| C# on .NET 10 | What the target employers ask for; current LTS |
| SQL Server Express + EF Core | SQL Server is the factory standard; EF Core for CRUD, raw T-SQL stored procedures for reports |
| ASP.NET Core Web API (controllers) | Familiar structure in enterprise C# teams |
| SignalR + Blazor Web App (interactive server) | Live dashboard in C# without a JavaScript build |
| ASP.NET Core Identity | Standard login and roles |
| Serilog | Structured logs with the machine code on every request |
| xUnit v3 + WebApplicationFactory | Integration tests through the real HTTP pipeline against real SQL Server |
| Microsoft Agent Framework + Microsoft.Extensions.AI + Ollama (phase 5) | Local, free model behind `IChatClient` |
| ML.NET (phase 6) | Time-series anomaly detection without Python |

All packages are free and open source; nothing runs in the cloud.

## Execution plan

Seven phases, one at a time, each ending with a working, tested system:

1. Schema and API: migrations, idempotent ingestion secured by machine API key, generator.
2. Live SCADA view: Blazor dashboard, SignalR, login with roles Operator / Engineer / Admin.
3. Traceability and reports: unit lookup, stored procedures for yield, downtime and OEE, reporting indexes, root-cause detection.
4. Connect the machine: HTTP publisher in PackInspect with store-and-forward, GitHub Actions with SQL Server tests.
5. AI root-cause assistant: behind `IChatClient`, read-only tools, every answer shows its data.
6. Predictive maintenance: ML.NET time-series anomaly detection.
7. Documentation: README, database diagram, test plan, demo script.

**MVP boundary**: phases 1 to 3 (ingest, live view, traceability and reports). Phases 4 and 5 make it a portfolio piece; phase 6 is optional; phase 7 is documentation.

**Out of scope**: scheduling and dispatching, recipe download to machines, SECS/GEM, multi-site, cloud deployment.
