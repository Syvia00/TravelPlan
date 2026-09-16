using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class AccommodationConfiguration : IEntityTypeConfiguration<Accommodation>
{
    public void Configure(EntityTypeBuilder<Accommodation> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Address).HasMaxLength(500);
        builder.Property(a => a.ConfirmationCode).HasMaxLength(100);

        builder.HasOne(a => a.Trip)
            .WithMany(t => t.Accommodations)
            .HasForeignKey(a => a.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Destination)
            .WithMany(d => d.Accommodations)
            .HasForeignKey(a => a.DestinationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
