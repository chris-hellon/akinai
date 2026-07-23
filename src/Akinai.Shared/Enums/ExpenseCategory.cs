namespace Akinai.Shared.Enums;

public enum ExpenseCategory
{
    Travel,
    OfficeSupplies,
    Software,
    Meals,
    Utilities,
    Marketing,
    ProfessionalServices,
    Equipment,
    Other
}

public static class ExpenseCategoryExtensions
{
    extension(ExpenseCategory category)
    {
        public string GetDisplayName() => category switch
        {
            ExpenseCategory.Travel => "Travel",
            ExpenseCategory.OfficeSupplies => "Office Supplies",
            ExpenseCategory.Software => "Software",
            ExpenseCategory.Meals => "Meals",
            ExpenseCategory.Utilities => "Utilities",
            ExpenseCategory.Marketing => "Marketing",
            ExpenseCategory.ProfessionalServices => "Professional Services",
            ExpenseCategory.Equipment => "Equipment",
            ExpenseCategory.Other => "Other",
            _ => category.ToString()
        };

        public string GetBadgeClass() => category switch
        {
            ExpenseCategory.Travel => "badge-info",
            ExpenseCategory.OfficeSupplies => "badge-neutral",
            ExpenseCategory.Software => "badge-primary",
            ExpenseCategory.Meals => "badge-success",
            ExpenseCategory.Utilities => "badge-warning",
            ExpenseCategory.Marketing => "badge-primary",
            ExpenseCategory.ProfessionalServices => "badge-info",
            ExpenseCategory.Equipment => "badge-neutral",
            ExpenseCategory.Other => "badge-neutral",
            _ => "badge-neutral"
        };
    }

    public static ExpenseCategory Parse(string category) => category switch
    {
        "Travel" => ExpenseCategory.Travel,
        "OfficeSupplies" => ExpenseCategory.OfficeSupplies,
        "Software" => ExpenseCategory.Software,
        "Meals" => ExpenseCategory.Meals,
        "Utilities" => ExpenseCategory.Utilities,
        "Marketing" => ExpenseCategory.Marketing,
        "ProfessionalServices" => ExpenseCategory.ProfessionalServices,
        "Equipment" => ExpenseCategory.Equipment,
        "Other" => ExpenseCategory.Other,
        _ => ExpenseCategory.Other
    };
}
