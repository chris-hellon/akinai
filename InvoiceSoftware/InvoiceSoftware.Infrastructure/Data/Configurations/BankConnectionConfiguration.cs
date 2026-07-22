using InvoiceSoftware.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> builder)
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
