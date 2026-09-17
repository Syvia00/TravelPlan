# Migrations: SQLite (dev) and SQL Server (Azure) side by side

## Why two migration sets

EF Core migrations bake in provider-specific column types at scaffold time (SQLite's
`TEXT`/`INTEGER` vs. SQL Server's `nvarchar`/`datetime2`/etc.) — replaying a SQLite-scaffolded
migration against SQL Server produces wrong or broken DDL, not a portable schema. One shared
`Migrations/` folder can only ever be correct for the provider it was scaffolded under.

So the app has two DbContext subclasses, each with its own migration history:

- `TravelPlanSqliteDbContext` → `TravelPlan.Api/Migrations/Sqlite/`
- `TravelPlanSqlServerDbContext` → `TravelPlan.Api/Migrations/SqlServer/`

Both derive from the real `TravelPlanDbContext`, add nothing, and exist purely so EF's migration
discovery (which keys off the exact context type via each migration's `[DbContext(typeof(...))]`
attribute) can tell the two histories apart. All repositories, services, and controllers depend
only on the base `TravelPlanDbContext` type — `Program.cs` registers whichever concrete subclass
matches the active `DatabaseProvider` setting via `AddDbContext<TravelPlanDbContext, TConcrete>`,
so this split is invisible to the rest of the app.

## Making a model change

1. Edit entities/configurations under `TravelPlan.Shared`/`TravelPlan.Api/Data/Configurations` as
   normal — one model, shared by both contexts.
2. Scaffold a migration for **each** provider, same name, from `TravelPlan.Api/`:

   ```bash
   dotnet ef migrations add <Name> --context TravelPlanSqliteDbContext -o Migrations/Sqlite
   dotnet ef migrations add <Name> --context TravelPlanSqlServerDbContext -o Migrations/SqlServer
   ```

   Scaffolding always uses the design-time factories in `TravelPlan.Api/Data/DesignTime/`, which
   hardcode the provider — this makes it deterministic regardless of appsettings/env config (the
   exact ambiguity that caused Session 9's `dotnet ef` tooling to silently pick the wrong
   provider).

3. Verify both apply cleanly to a fresh database:

   ```bash
   # SQLite — a throwaway local file
   dotnet ef database update --context TravelPlanSqliteDbContext \
     --connection "Data Source=/tmp/verify.db"

   # SQL Server — a local container, or a scratch Azure SQL database
   dotnet ef database update --context TravelPlanSqlServerDbContext \
     --connection "Server=tcp:localhost,1433;Initial Catalog=Verify;User ID=sa;Password=<...>;TrustServerCertificate=True;"
   ```

4. Confirm neither context reports drift:

   ```bash
   dotnet ef migrations has-pending-model-changes --context TravelPlanSqliteDbContext
   dotnet ef migrations has-pending-model-changes --context TravelPlanSqlServerDbContext
   ```

Steps 3–4 run automatically in CI (`.github/workflows/ci.yml`) against a real SQL Server service
container on every push/PR to `main` — a migration that only works for one provider fails the
build there, so this is enforced, not just documented. That workflow needs a `CI_MSSQL_SA_PASSWORD`
repo secret (any strong password satisfying SQL Server's complexity policy) set once in GitHub repo
settings.

## Applying to the real Azure SQL database

Runtime `Program.cs` calls `Database.Migrate()` on startup when `ASPNETCORE_ENVIRONMENT` is
`Development` (currently true in production too — see the dev-auth bypass note in the README).
Because the app now resolves to the correct concrete context type based on `DatabaseProvider`,
`Migrate()` picks up `Migrations/SqlServer/*` automatically when pointed at Azure SQL — no manual
`EnsureCreated()`/history-baselining is needed for new migrations going forward. That workaround was
only ever a one-time fix for the first deployment, made before this split existed.
