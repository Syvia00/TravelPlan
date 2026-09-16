using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class PhoneVerificationConfiguration : IEntityTypeConfiguration<PhoneVerification>
{
    public void Configure(EntityTypeBuilder<PhoneVerification> builder)
    {
        builder.Property(p => p.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(6).IsRequired();
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(p => p.PhoneNumber);
    }
}
