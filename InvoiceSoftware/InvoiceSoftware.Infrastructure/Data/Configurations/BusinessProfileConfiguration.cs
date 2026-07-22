using InvoiceSoftware.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
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

        builder.ToTable("BusinessProfiles");

        builder.HasKey(bp => bp.Id);

        builder.Property(bp => bp.CashBalance).HasPrecision(18, 2);

        builder.Property(bp => bp.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(bp => bp.CompanyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(bp => bp.TradingName)
            .HasMaxLength(200);

        builder.OwnsOne(bp => bp.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .HasMaxLength(256)
                .IsRequired();
        });

        builder.OwnsOne(bp => bp.Phone, phone =>
        {
            phone.Property(p => p.Value)
                .HasColumnName("Phone")
                .HasMaxLength(20);
        });

        builder.Property(bp => bp.Website)
            .HasMaxLength(256);

        builder.OwnsOne(bp => bp.Address, address =>
        {
            address.Property(a => a.Street1).HasColumnName("AddressStreet1").HasMaxLength(200);
            address.Property(a => a.Street2).HasColumnName("AddressStreet2").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("AddressCity").HasMaxLength(100);
            address.Property(a => a.State).HasColumnName("AddressState").HasMaxLength(50);
            address.Property(a => a.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("AddressCountry").HasMaxLength(100);
        });

        builder.Property(bp => bp.TaxNumber)
            .HasMaxLength(50);

        builder.Property(bp => bp.RegistrationNumber)
            .HasMaxLength(50);

        builder.Property(bp => bp.BankName)
            .HasMaxLength(200);

        builder.Property(bp => bp.BankAccountName)
            .HasMaxLength(200);

        builder.Property(bp => bp.BankAccountNumber)
            .HasMaxLength(50);

        builder.Property(bp => bp.BankSortCode)
            .HasMaxLength(20);

        builder.Property(bp => bp.BankIban)
            .HasMaxLength(50);

        builder.Property(bp => bp.BankSwift)
            .HasMaxLength(20);

        builder.Property(bp => bp.Logo)
            .HasMaxLength(500000); // 500KB max

        builder.Property(bp => bp.LogoContentType)
            .HasMaxLength(100);

        builder.Property(bp => bp.DefaultCurrency)
            .HasMaxLength(10)
            .HasDefaultValue("USD")
            .IsRequired();

        builder.Property(bp => bp.DefaultPaymentTermsDays)
            .HasDefaultValue(30);

        builder.Property(bp => bp.VndToDefaultCurrencyRate)
            .HasPrecision(18, 8);

        builder.Property(bp => bp.InvoiceNotes)
            .HasMaxLength(2000);

        builder.Property(bp => bp.InvoiceFooter)
            .HasMaxLength(500);

        builder.HasIndex(bp => bp.UserId)
            .IsUnique();
    }
}
