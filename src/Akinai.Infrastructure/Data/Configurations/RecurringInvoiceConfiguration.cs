using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class RecurringInvoiceConfiguration : IEntityTypeConfiguration<RecurringInvoice>
{
    public void Configure(EntityTypeBuilder<RecurringInvoice> builder)
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

        builder.ToTable("RecurringInvoices");

        builder.HasKey(ri => ri.Id);

        builder.Property(ri => ri.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(ri => ri.TemplateName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ri => ri.Notes)
            .HasMaxLength(2000);

        builder.Property(ri => ri.Terms)
            .HasMaxLength(4000);

        builder.Property(ri => ri.Footer)
            .HasMaxLength(1000);

        builder.Property(ri => ri.TaxRate)
            .HasPrecision(5, 2);

        builder.Property(ri => ri.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(ri => ri.Frequency)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ri => ri.StartDate).IsRequired();
        builder.Property(ri => ri.NextInvoiceDate).IsRequired();

        builder.HasOne(ri => ri.Client)
            .WithMany()
            .HasForeignKey(ri => ri.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(ri => ri.LineItems)
            .WithOne(li => li.RecurringInvoice)
            .HasForeignKey(li => li.RecurringInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(ri => ri.GeneratedInvoices)
            .WithMany();

        builder.HasIndex(ri => ri.UserId);
        builder.HasIndex(ri => ri.ClientId);
        builder.HasIndex(ri => ri.IsActive);
        builder.HasIndex(ri => ri.NextInvoiceDate);
    }
}
