using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class JobTaskConfiguration : IEntityTypeConfiguration<JobTask>
{
    public void Configure(EntityTypeBuilder<JobTask> builder)
    {
        builder.ToTable("JobTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.IsComplete)
            .IsRequired();

        builder.Property(t => t.Order)
            .IsRequired();

        builder.Property(t => t.CompletedAt);

        builder.HasIndex(t => t.JobId);
        builder.HasIndex(t => new { t.JobId, t.Order });
    }
}
