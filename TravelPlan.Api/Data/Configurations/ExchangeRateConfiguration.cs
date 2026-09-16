using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Data.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.Property(r => r.BaseCurrency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.QuoteCurrency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Rate).HasPrecision(18, 2);
    }
}
