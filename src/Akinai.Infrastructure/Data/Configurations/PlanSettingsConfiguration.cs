using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class PlanSettingsConfiguration : IEntityTypeConfiguration<PlanSettings>
{
    public void Configure(EntityTypeBuilder<PlanSettings> builder)
    {
        builder.ToTable("PlanSettings");
        builder.HasKey(p => p.Id);

        // One row per tier, enforced by the database rather than by convention —
        // two rows for "Pro" would make entitlements depend on row order.
        builder.HasIndex(p => p.Plan).IsUnique();

        builder.Property(p => p.Plan).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Tagline).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.CurrencySymbol).HasMaxLength(4).IsRequired();

        builder.Property(p => p.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(p => p.AnnualPrice).HasPrecision(18, 2);

        // Same 18,10 as AiUsage.CostUsd: the allowance is compared against sums
        // of sub-cent rows, so rounding it to pence would skew the comparison.
        builder.Property(p => p.AiSpendLimitUsd).HasPrecision(18, 10);

        builder.Property(p => p.Highlights).HasMaxLength(4000).IsRequired();
    }
}
