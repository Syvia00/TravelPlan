using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.ExternalAuthId).HasMaxLength(36);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Nullable-safe unique indexes: SQLite/SQL Server both treat multiple NULLs as
        // distinct for a unique index, so no filter is needed for that — only phone-only
        // or email-only accounts having both other columns null is fine.
        builder.HasIndex(u => u.ExternalAuthId).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.PhoneNumber).IsUnique();
    }
}
