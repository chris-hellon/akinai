using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).HasMaxLength(450).IsRequired();
        builder.Property(c => c.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(c => c.DocumentVersion).HasMaxLength(32).IsRequired();
        builder.Property(c => c.Source).HasMaxLength(64).IsRequired();

        // Long enough for an IPv6 address with an embedded IPv4 suffix.
        builder.Property(c => c.IpAddress).HasMaxLength(45);

        // The only question anyone asks of this table: what is the current position for
        // this user on this purpose, and what was it at some earlier date.
        builder.HasIndex(c => new { c.UserId, c.Purpose, c.RecordedAtUtc });

        // No foreign key to the user, and no cascade. A consent record has to outlive the
        // account it describes — "they agreed on this date" is the evidence that the mail
        // already sent was lawful, and deleting it with the account destroys exactly the
        // record that answers for what was done while it existed. Purging it is a
        // retention decision, taken deliberately, not a side effect of a delete.
    }
}
