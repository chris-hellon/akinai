using InvoiceSoftware.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
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
