using InvoiceSoftware.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class TranslationCacheEntryConfiguration : IEntityTypeConfiguration<TranslationCacheEntry>
{
    public void Configure(EntityTypeBuilder<TranslationCacheEntry> builder)
    {
        builder.ToTable("TranslationCache");
        builder.HasKey(e => e.Id);

        // 64 hex characters for SHA-256. Fixed width and non-Unicode so the
        // index stays narrow — this table is read on every invoice render.
        builder.Property(e => e.SourceHash).HasMaxLength(64).IsUnicode(false).IsRequired();

        builder.Property(e => e.SourceLanguage).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.TargetLanguage).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(e => e.SourceText).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.TranslatedText).HasMaxLength(4000).IsRequired();

        // The only lookup this table serves, and unique so a race between two
        // concurrent renders of the same invoice cannot leave two rows behind.
        builder.HasIndex(e => new { e.SourceHash, e.SourceLanguage, e.TargetLanguage })
            .IsUnique();
    }
}
