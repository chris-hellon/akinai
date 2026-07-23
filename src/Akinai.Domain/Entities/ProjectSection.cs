using Akinai.Domain.Common;

namespace Akinai.Domain.Entities;

public class ProjectSection : AggregateRoot, IAuditableEntity
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int Order { get; private set; }

    public Project Project { get; private set; } = null!;

    private readonly List<Job> _jobs = [];
    public IReadOnlyCollection<Job> Jobs => _jobs.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private ProjectSection() { }

    public static ProjectSection Create(Guid projectId, string name, string? description = null, int order = 0)
    {
        return new ProjectSection
        {
            ProjectId = projectId,
            Name = name,
            Description = description,
            Order = order
        };
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void SetOrder(int order)
    {
        Order = order;
    }
}
