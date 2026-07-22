using InvoiceSoftware.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class ClientLanguageSettingConfiguration : IEntityTypeConfiguration<ClientLanguageSetting>
{
    public void Configure(EntityTypeBuilder<ClientLanguageSetting> builder)
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

        builder.ToTable("ClientLanguageSettings");

        builder.HasKey(cls => cls.Id);

        builder.Property(cls => cls.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(cls => cls.PreferredLanguage)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(cls => cls.Client)
            .WithMany()
            .HasForeignKey(cls => cls.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cls => cls.UserId);
        builder.HasIndex(cls => new { cls.UserId, cls.ClientId })
            .IsUnique();
    }
}
