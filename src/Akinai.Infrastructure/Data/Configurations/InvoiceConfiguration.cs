using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.Property(i => i.SentToEmail)
            .HasMaxLength(320);

        // 64 hex characters for SHA-256; fixed length because it never varies.
        builder.Property(i => i.SentDocumentSha256)
            .HasMaxLength(64);

        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        builder.Property(i => i.IssueDate).IsRequired();
        builder.Property(i => i.DueDate).IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.TaxRate).HasPrecision(5, 2);
        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.Property(i => i.Currency).HasMaxLength(3).HasDefaultValue("USD");

        // Nullable on purpose: null means "inherit from the client or account",
        // which is a different statement from "English". Stored as a string so
        // the column stays readable and reordering the enum cannot corrupt it.
        builder.Property(i => i.Language).HasConversion<string>().HasMaxLength(20);

        builder.Property(i => i.Subtotal).HasPrecision(18, 2);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 2);
        builder.Property(i => i.Total).HasPrecision(18, 2);

        builder.HasOne(i => i.Client)
            .WithMany()
            .HasForeignKey(i => i.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Simple relationship: Invoice has many TimeEntries
        builder.HasMany(i => i.TimeEntries)
            .WithOne(te => te.Invoice)
            .HasForeignKey(te => te.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Invoice has many product LineItems
        builder.HasMany(i => i.LineItems)
            .WithOne(li => li.Invoice)
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Invoice has many per-currency total snapshots
        builder.HasMany(i => i.CurrencyTotals)
            .WithOne(ct => ct.Invoice)
            .HasForeignKey(ct => ct.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Invoice has many accepted payment currencies
        builder.HasMany(i => i.PaymentCurrencies)
            .WithOne(pc => pc.Invoice)
            .HasForeignKey(pc => pc.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.ClientId);
        builder.HasIndex(i => i.Status);
    }
}
