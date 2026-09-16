using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class TripMemoryConfiguration : IEntityTypeConfiguration<TripMemory>
{
    public void Configure(EntityTypeBuilder<TripMemory> builder)
    {
        builder.Property(m => m.ReportType).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Title).HasMaxLength(200).IsRequired();
        builder.Property(m => m.ReportData).IsRequired();
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(m => m.Trip)
            .WithMany(t => t.TripMemories)
            .HasForeignKey(m => m.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany(u => u.TripMemories)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
