using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class CurrencyPaymentSettingsConfiguration : IEntityTypeConfiguration<CurrencyPaymentSettings>
{
    public void Configure(EntityTypeBuilder<CurrencyPaymentSettings> builder)
    {
        // Ownership FK to the Identity user. Declared here — not as a navigation
        // property on the entity — so the Domain project stays free of any EF or
        // Identity dependency. Restrict, not Cascade: deleting an account must
        // not silently destroy invoices and financial records, and cascading
        // from 16 tables would trip SQL Server's multiple-cascade-paths rule.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => new { c.UserId, c.CurrencyCode }).IsUnique();

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(c => c.BankName).HasMaxLength(200);
        builder.Property(c => c.BankAccountName).HasMaxLength(200);
        builder.Property(c => c.BankAccountNumber).HasMaxLength(50);
        builder.Property(c => c.BankSortCode).HasMaxLength(20);
        builder.Property(c => c.BankIban).HasMaxLength(50);
        builder.Property(c => c.BankSwift).HasMaxLength(20);
        builder.Property(c => c.VietQrBankCode).HasMaxLength(20);

        // Correspondent bank details, for wires that cannot reach the
        // beneficiary bank directly. Same widths as their direct equivalents.
        builder.Property(c => c.IntermediaryBankName).HasMaxLength(200);
        builder.Property(c => c.IntermediaryBankSwift).HasMaxLength(20);
        builder.Property(c => c.IntermediaryAccountNumber).HasMaxLength(50);
        builder.Property(c => c.BankAddress).HasMaxLength(400);
        builder.Property(c => c.PaymentPurposeCode).HasMaxLength(200);
    }
}
