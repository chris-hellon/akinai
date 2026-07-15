using InvoiceSoftware.Shared.Enums;

namespace InvoiceSoftware.Shared.Services;

/// <summary>
/// Lightweight keyword-based auto-categorisation for bank transactions.
/// Used to pre-select a category when converting a transaction into an expense.
/// </summary>
public static class ExpenseCategoryRules
{
    // Ordered by priority — the first category with a matching keyword wins.
    private static readonly (ExpenseCategory Category, string[] Keywords)[] Rules =
    {
        (ExpenseCategory.Travel, new[]
        {
            "uber", "bolt", "grab", "lyft", "taxi", "cab", "train", "rail", "trainline",
            "flight", "airline", "ryanair", "easyjet", "british airways", "airport",
            "fuel", "petrol", "shell", "esso", "bp ", "parking", "ncp", "transport", "tfl", "metro", "tram"
        }),
        (ExpenseCategory.Software, new[]
        {
            "aws", "amazon web", "google cloud", "gcp", "azure", "github", "gitlab", "figma",
            "notion", "slack", "zoom", "adobe", "microsoft", "office 365", "openai", "anthropic",
            "jetbrains", "digitalocean", "vercel", "netlify", "cloudflare", "heroku", "stripe",
            "domain", "godaddy", "namecheap", "hosting", "hetzner", "linode", "atlassian", "jira",
            "dropbox", "1password", "spotify", "subscription", "saas"
        }),
        (ExpenseCategory.Meals, new[]
        {
            "restaurant", "cafe", "coffee", "starbucks", "costa", "pret", "greggs", "nero",
            "mcdonald", "kfc", "burger", "pizza", "deliveroo", "just eat", "justeat", "uber eats",
            "ubereats", "food", "dining", "lunch", "bar ", "pub", "brewery"
        }),
        (ExpenseCategory.Utilities, new[]
        {
            "electric", "energy", "octopus energy", "british gas", "edf", "eon", "water",
            "broadband", "internet", "virgin media", "sky ", "bt ", "vodafone", "o2", "ee ",
            "three", "giffgaff", "mobile", "phone", "utility", "council tax"
        }),
        (ExpenseCategory.Marketing, new[]
        {
            "facebook", "meta ", "google ads", "adwords", "linkedin", "twitter ads", "tiktok ads",
            "mailchimp", "sendgrid", "hubspot", "advertis", "campaign", "seo"
        }),
        (ExpenseCategory.ProfessionalServices, new[]
        {
            "accountant", "accounting", "bookkeep", "solicitor", "legal", "lawyer", "consult",
            "advisor", "notary", "audit", "freelance", "contractor"
        }),
        (ExpenseCategory.Equipment, new[]
        {
            "apple", "dell", "lenovo", "hp ", "currys", "pc world", "laptop", "monitor", "keyboard",
            "mouse", "hardware", "printer", "camera", "screen", "macbook", "iphone", "ipad"
        }),
        (ExpenseCategory.OfficeSupplies, new[]
        {
            "staples", "ryman", "wh smith", "stationery", "ikea", "office", "paper", "ink", "toner", "amazon"
        }),
    };

    public static ExpenseCategory Suggest(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return ExpenseCategory.Other;

        var text = description.ToLowerInvariant();
        foreach (var (category, keywords) in Rules)
        {
            if (keywords.Any(k => text.Contains(k)))
                return category;
        }
        return ExpenseCategory.Other;
    }
}
