using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TravelPlan.Api.Data.DesignTime;

// Used by `dotnet ef migrations add ... --context TravelPlanSqlServerDbContext` and by
// `dotnet ef database update --context TravelPlanSqlServerDbContext --connection "..."` (the
// --connection override replaces the placeholder below; scaffolding never actually connects).
public class TravelPlanSqlServerDbContextFactory : IDesignTimeDbContextFactory<TravelPlanSqlServerDbContext>
{
    public TravelPlanSqlServerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TRAVELPLAN_SQLSERVER_DESIGNTIME_CONNECTION")
            ?? "Server=tcp:localhost,1433;Initial Catalog=TravelPlanDb;User ID=sa;Password=Placeholder123!;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<TravelPlanSqlServerDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TravelPlanSqlServerDbContext(options);
    }
}
