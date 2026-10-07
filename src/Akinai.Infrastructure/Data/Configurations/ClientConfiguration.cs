using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
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

        builder.ToTable("Clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(c => c.PortalToken);

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.CompanyName)
            .HasMaxLength(200);

        builder.OwnsOne(c => c.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .HasMaxLength(256)
                .IsRequired();
        });

        builder.OwnsOne(c => c.Phone, phone =>
        {
            phone.Property(p => p.Value)
                .HasColumnName("Phone")
                .HasMaxLength(20);
        });

        builder.Property(c => c.TaxNumber)
            .HasMaxLength(64);

        // True for existing rows as well as new ones. Every client captured before this
        // column existed was a business, and defaulting them to consumer would silently
        // suppress reverse charge on invoices that need it.
        builder.Property(c => c.IsBusiness)
            .HasDefaultValue(true);

        builder.OwnsOne(c => c.BillingAddress, address =>
        {
            address.Property(a => a.Street1).HasColumnName("BillingStreet1").HasMaxLength(200);
            address.Property(a => a.Street2).HasColumnName("BillingStreet2").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("BillingCity").HasMaxLength(100);
            address.Property(a => a.State).HasColumnName("BillingState").HasMaxLength(50);
            address.Property(a => a.PostalCode).HasColumnName("BillingPostalCode").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("BillingCountry").HasMaxLength(100);
            address.Property(a => a.CountryCode).HasColumnName("BillingCountryCode").HasMaxLength(2);
        });

        builder.OwnsOne(c => c.DefaultHourlyRate, rate =>
        {
            rate.Property(r => r.Value)
                .HasColumnName("DefaultHourlyRate")
                .HasPrecision(18, 2)
                .IsRequired();
        });

        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.HasMany(c => c.Projects)
            .WithOne(p => p.Client)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.IsActive);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.PortalToken).IsUnique().HasFilter("[PortalToken] IS NOT NULL");
    }
}
