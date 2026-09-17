using Microsoft.EntityFrameworkCore;

namespace TravelPlan.Api.Data;

// Exists only so EF's migrations scaffolder/discovery has a distinct type to key migrations off
// of. Migrations are baked with each provider's own column types at scaffold time (SQLite's TEXT
// vs. SQL Server's nvarchar/datetime2/etc.) — one shared Migrations/ folder can't serve both
// providers correctly, so each gets its own context type and its own Migrations/<Provider> folder.
// Runtime code always depends on the base TravelPlanDbContext; Program.cs registers whichever of
// these two is active for AddDbContext<TravelPlanDbContext, TConcrete>, so repositories/services
// are unaffected by this split.
public class TravelPlanSqliteDbContext : TravelPlanDbContext
{
    public TravelPlanSqliteDbContext(DbContextOptions<TravelPlanSqliteDbContext> options)
        : base(options)
    {
    }
}
