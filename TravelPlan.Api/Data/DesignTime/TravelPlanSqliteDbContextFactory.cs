using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TravelPlan.Api.Data.DesignTime;

// Used by `dotnet ef migrations add ... --context TravelPlanSqliteDbContext`. Hardcodes the
// provider so scaffolding is deterministic regardless of appsettings/env config — Program.cs's
// own provider selection must never be involved in choosing what DDL a migration bakes in.
public class TravelPlanSqliteDbContextFactory : IDesignTimeDbContextFactory<TravelPlanSqliteDbContext>
{
    public TravelPlanSqliteDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TravelPlanSqliteDbContext>()
            .UseSqlite("Data Source=travelplan.dev.db")
            .Options;
        return new TravelPlanSqliteDbContext(options);
    }
}
