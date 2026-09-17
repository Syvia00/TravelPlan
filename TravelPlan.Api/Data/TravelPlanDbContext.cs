using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data;

public class TravelPlanDbContext : DbContext
{
    // Non-generic DbContextOptions so TravelPlanSqliteDbContext/TravelPlanSqlServerDbContext can
    // each pass through their own DbContextOptions<TDerived> — see those files for why the two
    // providers need distinct context types at all.
    public TravelPlanDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Destination> Destinations => Set<Destination>();

    public DbSet<PlanItem> PlanItems => Set<PlanItem>();

    public DbSet<Accommodation> Accommodations => Set<Accommodation>();

    public DbSet<TravelLeg> TravelLegs => Set<TravelLeg>();

    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();

    public DbSet<TripMemory> TripMemories => Set<TripMemory>();

    public DbSet<TripCollaborator> TripCollaborators => Set<TripCollaborator>();

    public DbSet<TripShareLink> TripShareLinks => Set<TripShareLink>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<PhoneVerification> PhoneVerifications => Set<PhoneVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TravelPlanDbContext).Assembly);

        // SQLite has no timezone-aware column type, so DateTime values read back come out as
        // Kind=Unspecified. All timestamps in this schema are written as UTC by application
        // code, so mark them Utc on the way out of the database rather than losing that fact.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }
    }
}
