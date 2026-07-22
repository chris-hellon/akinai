using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class UserLanguagePreferenceConfiguration : IEntityTypeConfiguration<UserLanguagePreference>
{
    public void Configure(EntityTypeBuilder<UserLanguagePreference> builder)
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

        builder.ToTable("UserLanguagePreferences");

        builder.HasKey(ulp => ulp.Id);

        builder.Property(ulp => ulp.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasIndex(ulp => ulp.UserId)
            .IsUnique();

        builder.Property(ulp => ulp.DefaultLanguage)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SupportedLanguage.English);

        builder.Property(ulp => ulp.InvoiceLanguage)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SupportedLanguage.English);

        builder.Property(ulp => ulp.EstimateLanguage)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SupportedLanguage.English);
    }
}
