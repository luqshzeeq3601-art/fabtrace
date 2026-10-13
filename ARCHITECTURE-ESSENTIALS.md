# FabTrace – Architecture essentials

The rules to keep in mind while changing code. Details are in `ARCHITECTURE.md`.

1. **Dependencies point inward**: Api -> Data -> Core -> Contracts. Core never references EF Core or ASP.NET Core. Contracts references nothing.
2. **Controllers are thin**: authenticate, validate, call a Core interface, map the outcome to a status code. No `DbContext` in controllers.
3. **Ingestion is idempotent**: every item has a machine-generated GUID with a unique index. New ingestion paths must follow "check, insert, retry on unique violation".
4. **Machine identity comes from the API key**, never from the payload.
5. **Payloads use codes, the database uses ids**. Resolve codes in the service; reject unknown master data per item.
6. **UTC in, UTC stored, UTC out** (`datetime2(3)`). Convert to factory time (UTC+8) only to work out shifts or to display.
7. **Schema changes only through EF Core migrations**, including stored procedures and indexes.
8. **Constraints belong in SQL Server** (unique, foreign key, check) as well as in code.
9. **Tests hit real SQL Server**. No in-memory or SQLite providers.
10. **Free and open-source packages only**; check the licence first. No FluentAssertions, MediatR or AutoMapper.
11. **No SQL from the model** (phase 5): the agent calls fixed, parameterised, read-only tools.
