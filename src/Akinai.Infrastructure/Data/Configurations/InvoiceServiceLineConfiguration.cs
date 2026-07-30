using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class InvoiceServiceLineConfiguration : IEntityTypeConfiguration<InvoiceServiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceServiceLine> builder)
    {
        builder.ToTable("InvoiceServiceLines");

        builder.HasKey(sl => sl.Id);

        builder.Property(sl => sl.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(sl => sl.Hours)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(sl => sl.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        // Cascade: a summary has no meaning without the invoice it summarises.
        builder.HasOne(sl => sl.Invoice)
            .WithMany(i => i.ServiceLines)
            .HasForeignKey(sl => sl.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(sl => sl.InvoiceId);
    }
}
