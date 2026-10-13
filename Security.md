# FabTrace – Security

## Secrets

- Nothing secret is committed. The connection strings in `appsettings.json` use Windows authentication and contain no password.
- Real secrets (a SQL login, production API keys, model API tokens) come from environment variables or `dotnet user-secrets`, never from files in the repository.
- Never print keys, connection strings with passwords, or machine details in logs, chat or commit messages.

## Machine authentication

- Each machine has one API key, sent in the `X-Api-Key` header.
- The database stores only the SHA-256 hash (`Machines.ApiKeyHash`, unique). Plain SHA-256 is acceptable because keys are long and random; user passwords are different and use ASP.NET Core Identity's salted, slow hash.
- The machine's identity is taken from the key. Payloads cannot name a machine, so one machine cannot write data as another, and a machine can only clear its own alarms.
- Setting `Machines.IsActive = 0` revokes a key immediately.
- Run behind HTTPS outside a development PC, because an API key in a header is readable on plain HTTP.

### Demo keys

The six demo machines use keys of the form `dev-key-<machine code>` (`DemoFactory.DevApiKey`). They are public on purpose so the generator works with no setup. They are seeded only when the API runs in the Development environment. A real deployment issues random keys via the Admin API (`POST /api/admin/machines/{machineCode}/api-key`) and never runs the seeder.

## User authentication

ASP.NET Core Identity with roles `Operator`, `Engineer` and `Admin`. Interactive endpoints (`/api/...`), SCADA status (`/api/scada/...`), and the live SignalR hub (`/hubs/factory`) require authenticated user bearer tokens; machine ingestion endpoints require valid machine API keys.

## Input handling

- Every request body is validated against the DTO attributes (required fields, length limits, enum values) before any code runs; batches are limited to 1000 items.
- All database access goes through EF Core, which sends values as parameters. No SQL is built by joining strings.
- Length limits in the DTOs equal the column sizes (`FieldLengths`), so oversized input is refused at the edge.
- Database constraints (unique, foreign key, check) are the last line of defence and are tested with raw SQL.

## AI assistant

- The model can only call a fixed set of read-only C# tools that run parameterised queries.
- SQL text produced by the model is never executed.
- Tool results are treated as data, and every answer shows the data it used.

## Destructive actions

- The test fixture drops a database only if its name ends in `_Test`.
- Dropping or resetting `FabTrace` needs the owner's confirmation.
