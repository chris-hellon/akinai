using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class SubscriptionEventConfiguration : IEntityTypeConfiguration<SubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionEvent> builder)
    {
        // Restrict, matching every other ownership FK: an account cannot be
        // deleted out from under its own billing history.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("SubscriptionEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(40).IsRequired();
        builder.Property(e => e.FromPlan).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ToPlan).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Cycle).HasMaxLength(20);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3);
        builder.Property(e => e.Note).HasMaxLength(500);

        builder.HasIndex(e => new { e.UserId, e.OccurredAtUtc });
    }
}

public class CheckoutSessionConfiguration : IEntityTypeConfiguration<CheckoutSession>
{
    public void Configure(EntityTypeBuilder<CheckoutSession> builder)
    {
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("CheckoutSessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId).HasMaxLength(450).IsRequired();
        builder.Property(s => s.Plan).HasMaxLength(40).IsRequired();
        builder.Property(s => s.Cycle).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Amount).HasPrecision(18, 2);
        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();
        builder.Property(s => s.ProviderSessionId).HasMaxLength(200);

        builder.HasIndex(s => new { s.UserId, s.CompletedAtUtc });
    }
}
