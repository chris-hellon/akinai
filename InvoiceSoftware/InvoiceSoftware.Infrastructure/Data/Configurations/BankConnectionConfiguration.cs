using InvoiceSoftware.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> builder)
    {
        builder.ToTable("BankConnections");
        builder.HasKey(bc => bc.Id);

        builder.Property(bc => bc.UserId).IsRequired().HasMaxLength(450);
        builder.Property(bc => bc.Provider).IsRequired().HasMaxLength(50);
        builder.Property(bc => bc.InstitutionId).IsRequired().HasMaxLength(200);
        builder.Property(bc => bc.InstitutionName).IsRequired().HasMaxLength(200);
        builder.Property(bc => bc.Reference).IsRequired().HasMaxLength(100);
        builder.Property(bc => bc.RequisitionId).IsRequired().HasMaxLength(200);
        builder.Property(bc => bc.ExternalAccountId).HasMaxLength(200);
        builder.Property(bc => bc.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(bc => bc.LastError).HasMaxLength(1000);

        builder.HasIndex(bc => bc.UserId);
        builder.HasIndex(bc => bc.Reference);
    }
}
