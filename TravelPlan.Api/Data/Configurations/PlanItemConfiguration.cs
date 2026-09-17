using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class PlanItemConfiguration : IEntityTypeConfiguration<PlanItem>
{
    public void Configure(EntityTypeBuilder<PlanItem> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.SourceUrl).HasMaxLength(500);

        builder.HasIndex(p => new { p.TripId, p.Date });

        builder.HasOne(p => p.Trip)
            .WithMany(t => t.PlanItems)
            .HasForeignKey(p => p.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        // See the matching comment in AccommodationConfiguration — same multiple-cascade-paths
        // conflict (Trips -> PlanItems direct Cascade vs. Trips -> Destinations -> PlanItems),
        // only surfaced once migrations first ran against real SQL Server. Restrict is safe
        // today (no Destinations-delete endpoint exists yet); a whole-Trip delete still works
        // via the direct cascade above.
        builder.HasOne(p => p.Destination)
            .WithMany(d => d.PlanItems)
            .HasForeignKey(p => p.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
