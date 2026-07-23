using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.TimeSuggestions;
using Akinai.Shared.Dtos.TimeSuggestions;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.TimeSuggestions;

public class GetTimeSuggestionsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetTimeSuggestions, List<TimeSuggestionDto>>
{
    public async Task<HttpResult<List<TimeSuggestionDto>>> Handle(
        GetTimeSuggestions request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<TimeSuggestionDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var limit = request.Limit > 0 ? request.Limit : 5;

        // Get recent time entries with their jobs and projects
        var query = db.TimeEntries
            .Include(te => te.Job)
                .ThenInclude(j => j.Project)
                    .ThenInclude(p => p.Client)
            .Where(te => te.UserId == userId);

        // Filter by client/project/job if specified
        if (request.ClientId.HasValue)
            query = query.Where(te => te.Job.Project.Client.Id == request.ClientId.Value);
        if (request.ProjectId.HasValue)
            query = query.Where(te => te.Job.ProjectId == request.ProjectId.Value);
        if (request.JobId.HasValue)
            query = query.Where(te => te.JobId == request.JobId.Value);

        var recentEntries = await query
            .OrderByDescending(te => te.Date)
            .Take(100)
            .ToListAsync(cancellationToken);

        // Group by description pattern and calculate suggestions
        var suggestions = recentEntries
            .GroupBy(te => new
            {
                te.JobId,
                Description = NormalizeDescription(te.Description)
            })
            .Select(g =>
            {
                var first = g.First();
                var count = g.Count();
                var avgHours = g.Average(te => te.Hours.Value);
                var lastUsed = g.Max(te => te.Date);

                // Calculate confidence based on frequency and recency
                var daysSinceLastUse = (DateOnly.FromDateTime(DateTime.Today).DayNumber - lastUsed.DayNumber);
                var recencyScore = Math.Max(0, 1 - (daysSinceLastUse / 30.0));
                var frequencyScore = Math.Min(1, count / 10.0);
                var confidence = (decimal)((recencyScore * 0.4) + (frequencyScore * 0.6));

                return new TimeSuggestionDto(
                    first.JobId,
                    first.Job.Name,
                    first.Job.ProjectId,
                    first.Job.Project.Name,
                    first.Job.Project.ClientId,
                    first.Job.Project.Client.Name,
                    first.Description ?? "",
                    Math.Round(avgHours, 2),
                    count,
                    lastUsed,
                    Math.Round(confidence, 2));
            })
            .OrderByDescending(s => s.ConfidenceScore)
            .ThenByDescending(s => s.UsageCount)
            .Take(limit)
            .ToList();

        return HttpResult<List<TimeSuggestionDto>>.Ok(suggestions);
    }

    private static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "";

        // Remove numbers (like hours, dates) and normalize whitespace
        var normalized = System.Text.RegularExpressions.Regex.Replace(description, @"\d+", "");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim().ToLowerInvariant();
    }
}
