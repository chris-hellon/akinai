using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
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

        // Owning user — matches the nvarchar(450) + index convention used by
        // every other tenant-owned entity, so it can be indexed.
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(e => e.UserId);

        builder.ToTable("Expenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.MerchantName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ExpenseDate).IsRequired();

        builder.Property(e => e.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(e => e.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Amount")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(e => e.TaxAmount).HasPrecision(18, 2);
        builder.Property(e => e.MerchantTaxNumber).HasMaxLength(100);
        builder.Property(e => e.GroupName).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        builder.HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Invoice)
            .WithMany()
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.RecurringExpense)
            .WithMany(re => re.GeneratedExpenses)
            .HasForeignKey(e => e.RecurringExpenseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.ExpenseDate);
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.ClientId);
        builder.HasIndex(e => e.IsBillable);
        builder.HasIndex(e => e.GroupName);
    }
}
