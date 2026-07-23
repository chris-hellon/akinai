using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class DigitalSignatureConfiguration : IEntityTypeConfiguration<DigitalSignature>
{
    public void Configure(EntityTypeBuilder<DigitalSignature> builder)
    {
        builder.ToTable("DigitalSignatures");

        builder.HasKey(ds => ds.Id);

        builder.Property(ds => ds.LinkedEntityType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ds => ds.LinkedEntityId)
            .IsRequired();

        builder.Property(ds => ds.SignatureData)
            .IsRequired();

        builder.Property(ds => ds.SignerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ds => ds.SignerEmail)
            .HasMaxLength(256);

        builder.Property(ds => ds.SignerIpAddress)
            .HasMaxLength(45);

        builder.Property(ds => ds.SignedAt)
            .IsRequired();

        builder.Property(ds => ds.InvalidationReason)
            .HasMaxLength(500);

        builder.HasIndex(ds => new { ds.LinkedEntityType, ds.LinkedEntityId })
            .IsUnique();
        builder.HasIndex(ds => ds.IsValid);
    }
}
