using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class Job : AggregateRoot, IAuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid? SectionId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Notes { get; private set; }
    public JobStatus Status { get; private set; } = JobStatus.Active;
    public JobPriority Priority { get; private set; } = JobPriority.Medium;
    public DateOnly? StartDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public Hours? EstimatedHours { get; private set; }
    public HourlyRate? HourlyRateOverride { get; private set; }

    // Alias for backwards compatibility
    public HourlyRate? OverrideHourlyRate => HourlyRateOverride;

    public Project Project { get; private set; } = null!;
    public ProjectSection? Section { get; private set; }

    private readonly List<TimeEntry> _timeEntries = [];
    public IReadOnlyCollection<TimeEntry> TimeEntries => _timeEntries.AsReadOnly();

    private readonly List<JobTask> _tasks = [];
    public IReadOnlyCollection<JobTask> Tasks => _tasks.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Job() { }

    public static Job Create(
        Guid projectId,
        string name,
        string? description = null,
        string? notes = null,
        decimal? estimatedHours = null,
        decimal? hourlyRateOverride = null,
        JobPriority priority = JobPriority.Medium,
        DateOnly? startDate = null,
        DateOnly? dueDate = null,
        Guid? sectionId = null)
    {
        return new Job
        {
            ProjectId = projectId,
            SectionId = sectionId,
            Name = name,
            Description = description,
            Notes = notes,
            EstimatedHours = estimatedHours.HasValue ? new Hours(estimatedHours.Value) : null,
            HourlyRateOverride = hourlyRateOverride.HasValue ? new HourlyRate(hourlyRateOverride.Value) : null,
            Priority = priority,
            StartDate = startDate,
            DueDate = dueDate
        };
    }

    public void Update(
        string name,
        string? description,
        string? notes,
        JobStatus status,
        decimal? estimatedHours = null,
        decimal? hourlyRateOverride = null,
        JobPriority priority = JobPriority.Medium,
        DateOnly? startDate = null,
        DateOnly? dueDate = null,
        Guid? sectionId = null)
    {
        Name = name;
        Description = description;
        Notes = notes;
        Status = status;
        EstimatedHours = estimatedHours.HasValue ? new Hours(estimatedHours.Value) : null;
        HourlyRateOverride = hourlyRateOverride.HasValue ? new HourlyRate(hourlyRateOverride.Value) : null;
        Priority = priority;
        StartDate = startDate;
        DueDate = dueDate;
        SectionId = sectionId;
    }

    public JobTask AddTask(string title, string? description = null)
    {
        var order = _tasks.Count;
        var task = JobTask.Create(Id, title, description, order);
        _tasks.Add(task);
        return task;
    }

    public void RemoveTask(Guid taskId)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            _tasks.Remove(task);
            ReorderTasks();
        }
    }

    private void ReorderTasks()
    {
        var order = 0;
        foreach (var task in _tasks.OrderBy(t => t.Order))
        {
            task.SetOrder(order++);
        }
    }

    public void SetOverrideRate(decimal hourlyRate)
        => HourlyRateOverride = new HourlyRate(hourlyRate);

    public void ClearOverrideRate()
        => HourlyRateOverride = null;

    public decimal GetEffectiveHourlyRate()
    {
        if (HourlyRateOverride is not null)
            return HourlyRateOverride.Value;
        if (Project != null ? Project.OverrideHourlyRate != null : false)
            return Project.OverrideHourlyRate.Value;
        return Project != null ? Project.Client != null ? Project.Client.DefaultHourlyRate?.Value ?? 0 : 0 : 0;
    }

    public void SetStatus(JobStatus status)
        => Status = status;

    public TimeEntry LogTime(DateOnly date, decimal hours, string? description, string userId)
    {
        if (Status != JobStatus.Active)
            throw new DomainException("Cannot log time to a non-active job");

        var timeEntry = TimeEntry.Create(Id, date, hours, description, userId);
        _timeEntries.Add(timeEntry);
        return timeEntry;
    }

    public Hours GetTotalLoggedHours()
    {
        var total = _timeEntries.Sum(te => te.Hours.Value);
        return new Hours(total);
    }

    public Hours GetUnbilledHours()
    {
        var total = _timeEntries.Where(te => !te.IsBilled).Sum(te => te.Hours.Value);
        return new Hours(total);
    }
}
