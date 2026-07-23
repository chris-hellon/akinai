using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.OwnsOne(p => p.HourlyRateOverride, rate =>
        {
            rate.Property(r => r.Value)
                .HasColumnName("HourlyRateOverride")
                .HasPrecision(18, 2);
        });

        // Ignore the backwards compatibility alias
        builder.Ignore(p => p.OverrideHourlyRate);

        builder.HasMany(p => p.Jobs)
            .WithOne(j => j.Project)
            .HasForeignKey(j => j.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Sections)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.ClientId);
        builder.HasIndex(p => p.Status);
    }
}
