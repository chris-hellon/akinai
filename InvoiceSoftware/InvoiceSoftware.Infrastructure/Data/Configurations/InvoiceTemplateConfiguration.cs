using InvoiceSoftware.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceSoftware.Infrastructure.Data.Configurations;

public class InvoiceTemplateConfiguration : IEntityTypeConfiguration<InvoiceTemplate>
{
    public void Configure(EntityTypeBuilder<InvoiceTemplate> builder)
    {
        builder.ToTable("InvoiceTemplates");

        builder.HasKey(it => it.Id);

        builder.Property(it => it.UserId)
            .HasMaxLength(450);

        builder.Property(it => it.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(it => it.Description)
            .HasMaxLength(500);

        builder.Property(it => it.TemplateType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(it => it.PrimaryColor)
            .HasMaxLength(7)
            .HasDefaultValue("#0e5c66");

        builder.Property(it => it.AccentColor)
            .HasMaxLength(7)
            .HasDefaultValue("#059669");

        builder.Property(it => it.TextColor)
            .HasMaxLength(7)
            .HasDefaultValue("#1F2937");

        builder.Property(it => it.BackgroundColor)
            .HasMaxLength(7)
            .HasDefaultValue("#FFFFFF");

        builder.Property(it => it.HeaderLayout)
            .HasMaxLength(20)
            .HasDefaultValue("standard");

        builder.Property(it => it.ItemsLayout)
            .HasMaxLength(20)
            .HasDefaultValue("table");

        builder.Property(it => it.FooterLayout)
            .HasMaxLength(20)
            .HasDefaultValue("standard");

        builder.Property(it => it.FontFamily)
            .HasMaxLength(100);

        builder.Property(it => it.CustomCss)
            .HasMaxLength(4000);

        builder.HasIndex(it => it.UserId);
        builder.HasIndex(it => it.IsDefault);
        builder.HasIndex(it => it.IsSystem);
    }
}
