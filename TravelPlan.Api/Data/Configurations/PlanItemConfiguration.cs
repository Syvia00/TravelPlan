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

        builder.HasOne(p => p.Destination)
            .WithMany(d => d.PlanItems)
            .HasForeignKey(p => p.DestinationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
