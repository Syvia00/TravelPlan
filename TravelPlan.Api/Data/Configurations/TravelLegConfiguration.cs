using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class TravelLegConfiguration : IEntityTypeConfiguration<TravelLeg>
{
    public void Configure(EntityTypeBuilder<TravelLeg> builder)
    {
        builder.Property(l => l.DepartureLocation).HasMaxLength(200).IsRequired();
        builder.Property(l => l.ArrivalLocation).HasMaxLength(200).IsRequired();
        builder.Property(l => l.TransportType).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.ConfirmationCode).HasMaxLength(100);

        builder.HasOne(l => l.Trip)
            .WithMany(t => t.TravelLegs)
            .HasForeignKey(l => l.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
