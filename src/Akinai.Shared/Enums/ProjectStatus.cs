namespace Akinai.Shared.Enums;

public enum ProjectStatus
{
    Active,
    OnHold,
    Completed,
    Archived
}

public static class ProjectStatusExtensions
{
    extension(ProjectStatus status)
    {
        public string GetDisplayName() => status switch
        {
            ProjectStatus.OnHold => "On Hold",
            _ => status.ToString()
        };

        public string GetBadgeClass() => status switch
        {
            ProjectStatus.Active => "badge-success",
            ProjectStatus.Completed => "badge-info",
            ProjectStatus.OnHold => "badge-warning",
            _ => "badge-neutral"
        };

        public string GetSmallBadgeClass() => status.GetBadgeClass();
    }

    public static ProjectStatus Parse(string status) => status switch
    {
        "Active" => ProjectStatus.Active,
        "OnHold" => ProjectStatus.OnHold,
        "Completed" => ProjectStatus.Completed,
        "Archived" => ProjectStatus.Archived,
        _ => ProjectStatus.Active
    };
}
