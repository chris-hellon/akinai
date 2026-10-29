using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class DunningReminderConfiguration : IEntityTypeConfiguration<DunningReminder>
{
    public void Configure(EntityTypeBuilder<DunningReminder> builder)
    {
        builder.ToTable("DunningReminders");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.Stage).HasMaxLength(32).IsRequired();
        builder.Property(r => r.ToEmail).HasMaxLength(320).IsRequired();
        builder.Property(r => r.Language).HasMaxLength(32).IsRequired();

        // The two questions the schedule asks: what has this invoice already had, and has
        // this client heard from us today.
        builder.HasIndex(r => r.InvoiceId);
        builder.HasIndex(r => new { r.ClientId, r.SentAtUtc });

        // Declared with no navigation on either side. A reminder is a record of fact about
        // an invoice, not part of the invoice aggregate — giving Invoice a Reminders
        // collection would invite loading it on every read of an invoice that has no
        // interest in it. But the foreign key itself has to exist: without this, EF saw a
        // bare Guid and created no constraint, so deleting an invoice left its reminders
        // behind as rows nothing could ever reach again.
        //
        // Cascade rather than restrict, because the delete is deliberately a hard delete
        // and the dialog promises it removes everything recorded against the invoice. A
        // reminder naming an invoice that no longer exists cannot be read by anything.
        //
        // ClientId stays a plain value with no constraint. It is denormalised from the
        // invoice (see the entity), and a client cannot be deleted out from under a
        // reminder anyway: reminders only exist for invoices, and invoices restrict
        // deletion of their client.
        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(r => r.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
