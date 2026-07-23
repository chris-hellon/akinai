using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
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
        builder.Property(re => re.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(re => re.UserId);

        builder.ToTable("RecurringExpenses");

        builder.HasKey(re => re.Id);

        builder.Property(re => re.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(re => re.MerchantName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(re => re.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(re => re.Amount, money =>
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

        builder.Property(re => re.TaxAmount).HasPrecision(18, 2);
        builder.Property(re => re.MerchantTaxNumber).HasMaxLength(100);
        builder.Property(re => re.GroupName).HasMaxLength(200);
        builder.Property(re => re.Notes).HasMaxLength(2000);

        builder.Property(re => re.Frequency)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(re => re.StartDate).IsRequired();
        builder.Property(re => re.NextExpenseDate).IsRequired();

        builder.HasOne(re => re.Client)
            .WithMany()
            .HasForeignKey(re => re.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(re => re.Project)
            .WithMany()
            .HasForeignKey(re => re.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(re => re.IsActive);
        builder.HasIndex(re => re.NextExpenseDate);
        builder.HasIndex(re => re.Category);
    }
}
