using Microsoft.EntityFrameworkCore;

namespace TravelPlan.Api.Data;

// See TravelPlanSqliteDbContext for why this split exists.
public class TravelPlanSqlServerDbContext : TravelPlanDbContext
{
    public TravelPlanSqlServerDbContext(DbContextOptions<TravelPlanSqlServerDbContext> options)
        : base(options)
    {
    }
}
