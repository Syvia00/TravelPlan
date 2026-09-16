using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Currency).HasMaxLength(3);
        builder.Property(t => t.TotalBudget).HasPrecision(18, 2);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(t => t.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(t => new { t.UserId, t.Status, t.StartDate });

        builder.HasOne(t => t.User)
            .WithMany(u => u.Trips)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
