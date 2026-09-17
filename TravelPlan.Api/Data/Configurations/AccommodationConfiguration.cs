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

        // erd.md originally specified SetNull here, same as Destinations -> PlanItems. But
        // Trips -> Accommodations (Cascade, above) and Trips -> Destinations (Cascade) ->
        // Destinations -> Accommodations together form the same "multiple cascade paths to one
        // table" shape SQL Server already rejects for Users -> TripCollaborators — this just
        // wasn't caught until migrations were first run against real SQL Server (SQLite never
        // enforces it). Restrict here is safe today because no Destinations-delete endpoint
        // exists yet; deleting a whole Trip is unaffected since the direct cascade above already
        // removes these rows. Once Destinations CRUD ships, deleting a single Destination will
        // need to explicitly null out DestinationId on its Accommodations first.
        builder.HasOne(a => a.Destination)
            .WithMany(d => d.Accommodations)
            .HasForeignKey(a => a.DestinationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
