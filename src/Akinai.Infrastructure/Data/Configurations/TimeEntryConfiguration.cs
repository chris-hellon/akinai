using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
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

        builder.ToTable("TimeEntries");

        builder.HasKey(te => te.Id);

        builder.Property(te => te.Date)
            .IsRequired();

        builder.OwnsOne(te => te.Hours, hours =>
        {
            hours.Property(h => h.Value)
                .HasColumnName("Hours")
                .HasPrecision(5, 2)
                .IsRequired();
        });

        builder.Property(te => te.Description)
            .HasMaxLength(500);

        builder.Property(te => te.UserId)
            .HasMaxLength(450)
            .IsRequired();

        // The rate frozen when the entry was invoiced. Nullable: an unbilled entry
        // has no billed rate and prices at the job's current one.
        builder.Property(te => te.BilledHourlyRate)
            .HasPrecision(18, 2);

        // Computed properties, not columns.
        builder.Ignore(te => te.IsBilled);
        builder.Ignore(te => te.BillingRate);

        builder.HasIndex(te => te.JobId);
        builder.HasIndex(te => te.Date);
        builder.HasIndex(te => te.UserId);
        builder.HasIndex(te => te.InvoiceId);
        builder.HasIndex(te => new { te.UserId, te.Date });
    }
}
