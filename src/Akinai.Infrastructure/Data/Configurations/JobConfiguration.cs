using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(j => j.Description)
            .HasMaxLength(2000);

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(j => j.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(j => j.StartDate);
        builder.Property(j => j.DueDate);

        builder.OwnsOne(j => j.EstimatedHours, hours =>
        {
            hours.Property(h => h.Value)
                .HasColumnName("EstimatedHours")
                .HasPrecision(10, 2);
        });

        builder.OwnsOne(j => j.HourlyRateOverride, rate =>
        {
            rate.Property(r => r.Value)
                .HasColumnName("HourlyRateOverride")
                .HasPrecision(18, 2);
        });

        // Ignore the backwards compatibility alias
        builder.Ignore(j => j.OverrideHourlyRate);

        builder.HasMany(j => j.TimeEntries)
            .WithOne(te => te.Job)
            .HasForeignKey(te => te.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.Tasks)
            .WithOne(t => t.Job)
            .HasForeignKey(t => t.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(j => j.ProjectId);
        builder.HasIndex(j => j.Status);
    }
}
