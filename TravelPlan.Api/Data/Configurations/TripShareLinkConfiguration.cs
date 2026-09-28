using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class TripShareLinkConfiguration : IEntityTypeConfiguration<TripShareLink>
{
    public void Configure(EntityTypeBuilder<TripShareLink> builder)
    {
        builder.Property(s => s.Token).HasMaxLength(64).IsRequired();
        // Stored as its underlying int, not HasConversion<string>() — see the matching note on
        // TripCollaboratorConfiguration.Role for why (a text column made a ">=" role comparison
        // translate into a SQL string comparison instead of a numeric one).
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(s => s.Token).IsUnique();

        builder.HasOne(s => s.Trip)
            .WithMany(t => t.ShareLinks)
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
