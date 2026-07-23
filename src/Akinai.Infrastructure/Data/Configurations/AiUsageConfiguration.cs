using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> builder)
    {
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("AiUsages");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserId).HasMaxLength(450).IsRequired();
        builder.Property(u => u.Feature).HasMaxLength(40).IsRequired();
        builder.Property(u => u.Model).HasMaxLength(60).IsRequired();

        // 18,10 rather than the 18,2 used for money elsewhere: one call can cost
        // a few thousandths of a cent, and 18,2 would round every row to zero.
        builder.Property(u => u.CostUsd).HasPrecision(18, 10);

        // The cap query is "sum cost for this user since the 1st" — this index
        // is exactly that shape, and it runs before every AI call.
        builder.HasIndex(u => new { u.UserId, u.OccurredAtUtc });
    }
}
