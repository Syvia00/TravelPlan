using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class TripCollaboratorConfiguration : IEntityTypeConfiguration<TripCollaborator>
{
    public void Configure(EntityTypeBuilder<TripCollaborator> builder)
    {
        // Stored as its underlying int (Viewer=0 < Editor=1), not HasConversion<string>() — a
        // text column made ITripAccessService.HasAccessAsync's ">=" privilege check translate
        // into a SQL string comparison ("Editor" < "Viewer" alphabetically, the inverse of their
        // intended order), letting a Viewer collaborator pass an Editor-only check. Fixed in that
        // service for now by comparing in memory instead, but storing the enum numerically removes
        // the hazard for every future comparison too, not just the ones audited so far.
        builder.Property(c => c.InvitedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // One invite per (trip, user) — TripCollaboratorsController.Create checks this
        // explicitly before insert (for a clean validation error instead of a raw DbUpdateException),
        // but the index is the actual integrity guarantee under concurrent invites.
        builder.HasIndex(c => new { c.TripId, c.UserId }).IsUnique();

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
