using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class TripCollaboratorConfiguration : IEntityTypeConfiguration<TripCollaborator>
{
    public void Configure(EntityTypeBuilder<TripCollaborator> builder)
    {
        builder.Property(c => c.Role).HasConversion<string>().HasMaxLength(10);
        builder.Property(c => c.InvitedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Users -> Trips and Trips -> TripCollaborators are both Cascade, so a direct Cascade
        // from Users -> TripCollaborators too would create two cascade paths to the same table.
        // SQLite doesn't enforce this (why this built clean in Session 2), but SQL Server
        // rejects it at migration time — hence Restrict here. The app must explicitly delete a
        // user's TripCollaborators rows before deleting the Users row itself; the Trips cascade
        // still covers the normal case of a trip being deleted.
        builder.HasOne(c => c.Trip)
            .WithMany(t => t.Collaborators)
            .HasForeignKey(c => c.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany(u => u.TripCollaborations)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
