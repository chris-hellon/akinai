using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class InvoiceCurrencyTotalConfiguration : IEntityTypeConfiguration<InvoiceCurrencyTotal>
{
    public void Configure(EntityTypeBuilder<InvoiceCurrencyTotal> builder)
    {
        builder.ToTable("InvoiceCurrencyTotals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.HasIndex(x => new { x.InvoiceId, x.Currency }).IsUnique();
        builder.HasIndex(x => x.Currency);
    }
}
