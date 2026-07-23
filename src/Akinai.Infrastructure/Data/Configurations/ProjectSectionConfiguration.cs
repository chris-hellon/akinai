using Akinai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Akinai.Infrastructure.Data.Configurations;

public class ProjectSectionConfiguration : IEntityTypeConfiguration<ProjectSection>
{
    public void Configure(EntityTypeBuilder<ProjectSection> builder)
    {
        builder.ToTable("ProjectSections");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        builder.HasMany(s => s.Jobs)
            .WithOne(j => j.Section)
            .HasForeignKey(j => j.SectionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(s => s.ProjectId);
        builder.HasIndex(s => s.Order);
    }
}
