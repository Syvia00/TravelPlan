using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class DestinationConfiguration : IEntityTypeConfiguration<Destination>
{
    public void Configure(EntityTypeBuilder<Destination> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.CountryCode).HasMaxLength(3).IsRequired();

        builder.HasIndex(d => new { d.UserId, d.CountryCode, d.EntryDate });

        builder.HasOne(d => d.Trip)
            .WithMany(t => t.Destinations)
            .HasForeignKey(d => d.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft ownership — deleting a user must not cascade into their logged destinations.
        builder.HasOne(d => d.User)
            .WithMany(u => u.Destinations)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
