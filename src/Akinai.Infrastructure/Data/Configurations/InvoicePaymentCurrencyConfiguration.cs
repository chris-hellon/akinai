using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class InvoicePaymentCurrencyConfiguration : IEntityTypeConfiguration<InvoicePaymentCurrency>
{
    public void Configure(EntityTypeBuilder<InvoicePaymentCurrency> builder)
    {
        builder.ToTable("InvoicePaymentCurrencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        // One row per currency per invoice. The owner ticking EUR twice is not a thing
        // the UI can do, but the constraint is what makes that true rather than hoped.
        builder.HasIndex(x => new { x.InvoiceId, x.Currency }).IsUnique();
    }
}
