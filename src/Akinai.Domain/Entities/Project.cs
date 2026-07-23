using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class Project : AggregateRoot, IAuditableEntity
{
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public ProjectStatus Status { get; private set; } = ProjectStatus.Active;
    public HourlyRate? HourlyRateOverride { get; private set; }

    // Alias for backwards compatibility
    public HourlyRate? OverrideHourlyRate => HourlyRateOverride;

    public Client Client { get; private set; } = null!;

    private readonly List<Job> _jobs = [];
    public IReadOnlyCollection<Job> Jobs => _jobs.AsReadOnly();

    private readonly List<ProjectSection> _sections = [];
    public IReadOnlyCollection<ProjectSection> Sections => _sections.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Project() { }

    public static Project Create(
        Guid clientId,
        string name,
        string? description = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        decimal? hourlyRateOverride = null)
    {
        if (endDate.HasValue && startDate.HasValue && endDate < startDate)
            throw new DomainException("End date cannot be before start date");

        return new Project
        {
            ClientId = clientId,
            Name = name,
            Description = description,
            StartDate = startDate,
            EndDate = endDate,
            HourlyRateOverride = hourlyRateOverride.HasValue ? new HourlyRate(hourlyRateOverride.Value) : null
        };
    }

    public void Update(
        string name,
        string? description,
        ProjectStatus status,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        decimal? hourlyRateOverride = null)
    {
        if (endDate.HasValue && startDate.HasValue && endDate < startDate)
            throw new DomainException("End date cannot be before start date");

        Name = name;
        Description = description;
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        HourlyRateOverride = hourlyRateOverride.HasValue ? new HourlyRate(hourlyRateOverride.Value) : null;
    }

    public void SetOverrideRate(decimal hourlyRate)
        => HourlyRateOverride = new HourlyRate(hourlyRate);

    public void ClearOverrideRate()
        => HourlyRateOverride = null;

    public void SetStatus(ProjectStatus status)
        => Status = status;

    public Job AddJob(
        string name,
        string? description = null,
        decimal? estimatedHours = null)
    {
        var job = Job.Create(Id, name, description, notes: null, estimatedHours: estimatedHours);
        _jobs.Add(job);
        return job;
    }

    public ProjectSection AddSection(string name, string? description = null)
    {
        var order = _sections.Count;
        var section = ProjectSection.Create(Id, name, description, order);
        _sections.Add(section);
        return section;
    }
}
