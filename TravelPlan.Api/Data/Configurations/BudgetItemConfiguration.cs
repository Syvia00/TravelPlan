using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class BudgetItemConfiguration : IEntityTypeConfiguration<BudgetItem>
{
    public void Configure(EntityTypeBuilder<BudgetItem> builder)
    {
        builder.Property(b => b.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(b => b.Description).HasMaxLength(500).IsRequired();
        builder.Property(b => b.Amount).HasPrecision(18, 2);
        builder.Property(b => b.Currency).HasMaxLength(3).HasDefaultValue("USD");

        builder.HasIndex(b => new { b.TripId, b.Category });

        builder.HasOne(b => b.Trip)
            .WithMany(t => t.BudgetItems)
            .HasForeignKey(b => b.TripId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
