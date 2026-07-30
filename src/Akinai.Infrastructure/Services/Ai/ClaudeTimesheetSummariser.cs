using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Akinai.Shared.Ai;
using Akinai.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akinai.Infrastructure.Services.Ai;

/// <summary>
/// Asks Claude how a timesheet should read on an invoice.
///
/// The reply is forced through a tool schema rather than requested as prose,
/// so the shape is the model's problem and parsing is not ours. Even so the
/// result is treated as untrusted: <c>TimesheetGrouping</c> re-derives every
/// number from the entries and rejects a grouping that does not cover each one
/// exactly once. This class is therefore allowed to be wrong without an
/// incorrect invoice being possible.
/// </summary>
public class ClaudeTimesheetSummariser(
    IOptions<ClaudeOptions> options,
    ClaudeClientFactory clients,
    IEntitlementService entitlements,
    IAiBudgetService budget,
    ILogger<ClaudeTimesheetSummariser> logger) : ITimesheetSummariser
{
    private readonly ClaudeOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    private const string ToolName = "propose_invoice_lines";

    /// <summary>
    /// Generous enough for a long timesheet, bounded so a pathological reply
    /// cannot run up a bill. Twenty groups of prose is far more than any real
    /// invoice needs.
    /// </summary>
    private const int MaxTokens = 2000;

    public async Task<TimesheetSummary> SummariseAsync(
        string userId,
        string clientName,
        IReadOnlyList<TimesheetEntry> entries,
        string tone,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            return TimesheetSummary.Unavailable("AI summarising is not configured on this deployment.");

        if (entries.Count == 0)
            return TimesheetSummary.Unavailable("There is no tracked time on this invoice.");

        // Nothing to gain and a call to pay for.
        if (entries.Count == 1)
            return TimesheetSummary.Unavailable("There is only one entry — nothing to group.");

        // Plan first, then allowance. Enforced here and not only in the UI:
        // hiding a button has never stopped a crafted request, and this one
        // spends money.
        var plan = await entitlements.GetAsync(userId, cancellationToken);

        if (!plan.HasAiFeatures)
        {
            return TimesheetSummary.Unavailable(
                "Summarising a timesheet is a Pro feature.");
        }

        if (!plan.CanUseAi)
        {
            return TimesheetSummary.Unavailable(
                "This month's AI allowance is used up. It resets on the 1st.");
        }

        try
        {
            var client = clients.Get();
            if (client is null)
                return TimesheetSummary.Unavailable("AI summarising is not configured on this deployment.");

            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = MaxTokens,
                // Base rules plus one line of tone. The tone only shifts the
                // voice; every rule about grouping, honesty and not stating
                // numbers is in the base and applies whatever the tone.
                System = $"{SystemPrompt}\n\n{Shared.Ai.SummaryTone.Instruction(tone)}",
                Tools = [BuildTool()],
                // Forces the tool rather than letting it answer in prose.
                ToolChoice = new ToolChoiceTool { Name = ToolName },
                Messages =
                [
                    new() { Role = Role.User, Content = BuildTimesheet(clientName, entries) },
                ],
            }, cancellationToken: cancellationToken);

            // Priced from the provider's own token counts, never an estimate.
            await budget.RecordTokensAsync(
                userId,
                AiFeature.LineItemWriting,
                _options.Model,
                (int)response.Usage.InputTokens,
                (int)response.Usage.OutputTokens,
                cancellationToken);

            var groups = ReadGroups(response);

            if (groups.Count == 0)
            {
                logger.LogWarning("Summariser returned no usable groups for {UserId}.", userId);
                return TimesheetSummary.Unavailable("The summary came back empty. Please try again.");
            }

            return new TimesheetSummary(groups, null);
        }
        catch (Exception ex)
        {
            // Deliberately broad, and deliberately not rethrown. An invoice must
            // still be sendable when the provider is down; the caller keeps the
            // unsummarised rows.
            logger.LogError(ex, "Timesheet summarisation failed for {UserId}.", userId);
            return TimesheetSummary.Unavailable("Could not reach the summarising service. Please try again.");
        }
    }

    private const string SystemPrompt = """
        You rewrite a freelancer's timesheet as the few lines their client should
        see on an invoice.

        A timesheet has one row per sitting. An invoice should describe the work.
        Group the entries into the smallest number of lines that still tells the
        client honestly what they are paying for — usually between two and six.

        Rules:
        - Every entry id must appear in exactly one group. Never drop one, never
          repeat one.
        - Group by the work, not by the date. Several days on the same feature
          are one line.
        - Write each description for the client, not for the freelancer. Expand
          internal shorthand and drop internal codenames, ticket numbers and tool
          names the client would not recognise.
        - Keep it factual. Do not embellish, do not claim outcomes, and do not
          invent work that is not in the entries.
        - Never state hours, rates or amounts in a description. Those are added
          from the tracked data afterwards.
        - Write in the same language as the entries.
        """;

    /// <summary>
    /// The timesheet as plain text. Hours are included because they tell the
    /// model what was substantial and what was incidental — it is asked not to
    /// repeat them, and could not affect the invoice if it did.
    /// </summary>
    private static string BuildTimesheet(string clientName, IReadOnlyList<TimesheetEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append("Client: ").AppendLine(clientName);
        sb.AppendLine().AppendLine("Entries:");

        foreach (var e in entries)
        {
            sb.Append("- id=").Append(e.TimeEntryId)
              .Append(" | ").Append(e.Date.ToString("yyyy-MM-dd"))
              .Append(" | ").Append(e.BillableHours.ToString("0.##")).Append("h")
              .Append(" | project: ").Append(e.ProjectName)
              .Append(" | job: ").Append(e.JobName);

            if (!string.IsNullOrWhiteSpace(e.Description))
                sb.Append(" | note: ").Append(e.Description);

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static Tool BuildTool() => new()
    {
        Name = ToolName,
        Description = "Return the grouped invoice lines for this timesheet.",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["lines"] = JsonSerializer.SerializeToElement(new
                {
                    type = "array",
                    description = "The invoice lines, in the order they should appear.",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            description = new
                            {
                                type = "string",
                                description = "What the client reads. No hours, rates or amounts.",
                            },
                            entry_ids = new
                            {
                                type = "array",
                                description = "Ids of the entries in this line.",
                                items = new { type = "string" },
                            },
                        },
                        required = new[] { "description", "entry_ids" },
                    },
                }),
            },
            Required = ["lines"],
        },
    };

    /// <summary>
    /// Pulls the groups out of the tool call. Anything malformed is skipped
    /// rather than throwing — a partial result is still rejected downstream if
    /// it does not cover every entry, so being lenient here is safe.
    /// </summary>
    private static List<ProposedServiceLine> ReadGroups(Message response)
    {
        var groups = new List<ProposedServiceLine>();

        foreach (var block in response.Content)
        {
            if (!block.TryPickToolUse(out var toolUse) || toolUse!.Name != ToolName)
                continue;

            if (!toolUse.Input.TryGetValue("lines", out var lines)
                || lines.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var line in lines.EnumerateArray())
            {
                if (!line.TryGetProperty("description", out var description)
                    || description.ValueKind != JsonValueKind.String)
                    continue;

                if (!line.TryGetProperty("entry_ids", out var ids)
                    || ids.ValueKind != JsonValueKind.Array)
                    continue;

                var parsed = new List<Guid>();
                foreach (var id in ids.EnumerateArray())
                {
                    if (id.ValueKind == JsonValueKind.String
                        && Guid.TryParse(id.GetString(), out var entryId))
                        parsed.Add(entryId);
                }

                if (parsed.Count > 0)
                    groups.Add(new ProposedServiceLine(description.GetString()!, parsed));
            }
        }

        return groups;
    }
}
