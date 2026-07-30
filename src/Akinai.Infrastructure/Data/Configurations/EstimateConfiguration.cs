using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class EstimateConfiguration : IEntityTypeConfiguration<Estimate>
{
    public void Configure(EntityTypeBuilder<Estimate> builder)
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

        builder.ToTable("Estimates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(e => e.EstimateNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => e.EstimateNumber)
            .IsUnique();

        builder.Property(e => e.EstimateDate).IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Stored as its name, like the invoice's, so the column stays readable
        // and reordering the enum cannot silently repoint existing rows.
        builder.Property(e => e.Language)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.TaxRate)
            .HasPrecision(5, 2);

        builder.Property(e => e.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.Terms)
            .HasMaxLength(4000);

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ConvertedInvoice)
            .WithMany()
            .HasForeignKey(e => e.ConvertedInvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.LineItems)
            .WithOne(li => li.Estimate)
            .HasForeignKey(li => li.EstimateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.PublicAccessToken);
    }
}
