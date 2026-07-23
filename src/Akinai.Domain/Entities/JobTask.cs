using Akinai.Domain.Common;

namespace Akinai.Domain.Entities;

public class JobTask : BaseEntity, IAuditableEntity
{
    public Guid JobId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsComplete { get; private set; }
    public int Order { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public Job Job { get; private set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private JobTask() { }

    public static JobTask Create(Guid jobId, string title, string? description = null, int order = 0)
    {
        return new JobTask
        {
            JobId = jobId,
            Title = title,
            Description = description,
            Order = order,
            IsComplete = false
        };
    }

    public void Update(string title, string? description)
    {
        Title = title;
        Description = description;
    }

    public void SetOrder(int order)
    {
        Order = order;
    }

    public void MarkComplete()
    {
        if (!IsComplete)
        {
            IsComplete = true;
            CompletedAt = DateTime.UtcNow;
        }
    }

    public void MarkIncomplete()
    {
        IsComplete = false;
        CompletedAt = null;
    }

    public void ToggleComplete()
    {
        if (IsComplete)
            MarkIncomplete();
        else
            MarkComplete();
    }
}
