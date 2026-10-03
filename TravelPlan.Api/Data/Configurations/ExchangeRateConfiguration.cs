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

        // erd.md documents decimal(18,2) — same precision as BudgetItems.Amount/Trips.TotalBudget,
        // which makes sense for money but not for a rate: real Frankfurter rates routinely carry
        // 4-5 decimal places (e.g. CHF 0.82664), and (18,2) would silently round that to 0.83 on
        // every write — a ~0.4% error compounded into every converted amount. Using (18,6) here
        // instead, confirmed against this table being empty in production so there's no existing
        // data to reconcile (see deployment-runbook.md's free-tier migration for the same
        // empty-table reasoning). erd.md's annotation is being corrected to match.
        builder.Property(r => r.Rate).HasPrecision(18, 6);

        // One cached rate per (base, quote) pair — ExchangeRateService upserts on refresh rather
        // than accumulating a new row per fetch; this is the actual integrity guarantee for that,
        // not just an optimization.
        builder.HasIndex(r => new { r.BaseCurrency, r.QuoteCurrency }).IsUnique();
    }
}
