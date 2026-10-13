# FabTrace – Tools the agent may use

## Shell commands

| Command | Use |
|---|---|
| `dotnet build`, `dotnet test`, `dotnet run --project <project>` | Build, test, run |
| `dotnet ef migrations add / script / list`, `dotnet ef database update` | Schema changes (local tool, `dotnet tool restore`) |
| `dotnet add <project> package <name>` | Only after the licence check below |
| `sqlcmd -S .\SQLEXPRESS -E -C -d FabTrace -Q "<query>"` | Read-only inspection of data and query plans |
| `git status`, `git diff`, `git log` | Inspect; commit only when asked |

Ask first: `dotnet ef database drop`, any `DELETE` / `UPDATE` / `DROP` against `FabTrace`, anything outside this folder.

## Local endpoints

| Service | Address |
|---|---|
| FabTrace API | `http://localhost:5080` (routes in `Data.md`, examples in `src/FabTrace.Api/FabTrace.Api.http`) |
| FabTrace Web | `http://localhost:5176` |
| SQL Server Express | `.\SQLEXPRESS`, Windows authentication, databases `FabTrace` and `FabTrace_Test` |
| Ollama (phase 5) | `http://localhost:11434` |

## Approved packages

Licence taken from each package's nuspec at the version in `Directory.Packages.props`.

| Package | Licence | Used for |
|---|---|---|
| Microsoft.EntityFrameworkCore.SqlServer | MIT | ORM, migrations |
| Microsoft.EntityFrameworkCore.Design | MIT | `dotnet ef` design-time support |
| dotnet-ef (tool) | MIT | Migration commands |
| Microsoft.AspNetCore.OpenApi | MIT | OpenAPI document |
| Serilog.AspNetCore | Apache-2.0 | Structured logging (console and file sinks included) |
| xunit.v3 | Apache-2.0 | Tests, on Microsoft Testing Platform |
| Microsoft.AspNetCore.Mvc.Testing | MIT | `WebApplicationFactory` integration tests |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | MIT | ASP.NET Core Identity store for EF Core |
| Microsoft.AspNetCore.SignalR.Client | MIT | SignalR real-time client for Blazor Web |
| Microsoft.Extensions.AI | MIT | Generic AI abstraction (`IChatClient`, tool invocation) |
| OllamaSharp | MIT | Local Ollama connector implementing `IChatClient` |
| Microsoft.ML | MIT | Core ML.NET framework for on-device machine learning |
| Microsoft.ML.TimeSeries | MIT | ML.NET time-series anomaly and change-point detection |

Not allowed: FluentAssertions, MediatR, AutoMapper (commercial licences), and any package that is not free and open source.

## Documentation lookups

Use the Microsoft Learn documentation tools for anything version-sensitive (.NET 10, EF Core 10, and all of phase 5) instead of relying on memory.
