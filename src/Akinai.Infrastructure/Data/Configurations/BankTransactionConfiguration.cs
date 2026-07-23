using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
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

        builder.ToTable("BankTransactions");

        builder.HasKey(bt => bt.Id);

        builder.Property(bt => bt.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(bt => bt.TransactionDate)
            .IsRequired();

        builder.Property(bt => bt.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(bt => bt.Amount)
            .HasColumnName("Amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(bt => bt.Currency)
            .HasColumnName("Currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(bt => bt.Reference)
            .HasMaxLength(100);

        builder.Property(bt => bt.BankAccountName)
            .HasMaxLength(200);

        builder.Property(bt => bt.MatchConfidence)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(bt => bt.MatchNotes)
            .HasMaxLength(500);

        builder.Property(bt => bt.SourceFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(bt => bt.IgnoreReason)
            .HasMaxLength(500);

        builder.Property(bt => bt.ExternalId)
            .HasMaxLength(255);

        builder.HasOne(bt => bt.MatchedInvoice)
            .WithMany()
            .HasForeignKey(bt => bt.MatchedInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(bt => bt.UserId);
        builder.HasIndex(bt => bt.TransactionDate);
        builder.HasIndex(bt => bt.MatchedInvoiceId);
        builder.HasIndex(bt => bt.ExpenseId);
        builder.HasIndex(bt => bt.IsIgnored);
        builder.HasIndex(bt => bt.ExternalId);
    }
}
