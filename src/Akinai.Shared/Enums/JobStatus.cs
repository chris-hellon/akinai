namespace Akinai.Shared.Enums;

public enum JobStatus
{
    Active,
    OnHold,
    Completed
}

public static class JobStatusExtensions
{
    extension(JobStatus status)
    {
        public string GetDisplayName() => status switch
        {
            JobStatus.OnHold => "On Hold",
            _ => status.ToString()
        };

        public string GetBadgeClass() => status switch
        {
            JobStatus.Active => "badge-success",
            JobStatus.Completed => "badge-info",
            JobStatus.OnHold => "badge-warning",
            _ => "badge-neutral"
        };

        public string GetSmallBadgeClass() => status.GetBadgeClass();
    }

    public static JobStatus Parse(string status) => status switch
    {
        "Active" => JobStatus.Active,
        "OnHold" => JobStatus.OnHold,
        "Completed" => JobStatus.Completed,
        _ => JobStatus.Active
    };
}
