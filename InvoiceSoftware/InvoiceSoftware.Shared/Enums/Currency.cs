namespace InvoiceSoftware.Shared.Enums;

public enum Currency
{
    GBP,
    USD,
    EUR,
    AUD,
    CAD,
    VND,
    RUB
}

public static class CurrencyExtensions
{
    extension(Currency currency)
    {
        public string GetSymbol() => currency switch
        {
            Currency.GBP => "\u00a3",
            Currency.USD => "$",
            Currency.EUR => "\u20ac",
            Currency.AUD => "A$",
            Currency.CAD => "C$",
            Currency.VND => "\u20ab",
            Currency.RUB => "\u20bd",
            _ => currency.ToString()
        };

        public string GetDisplayName() => currency switch
        {
            Currency.GBP => "British Pound",
            Currency.USD => "US Dollar",
            Currency.EUR => "Euro",
            Currency.AUD => "Australian Dollar",
            Currency.CAD => "Canadian Dollar",
            Currency.VND => "Vietnamese Dong",
            Currency.RUB => "Russian Ruble",
            _ => currency.ToString()
        };
    }

    public static Currency ParseCurrency(string code) => code switch
    {
        "GBP" => Currency.GBP,
        "USD" => Currency.USD,
        "EUR" => Currency.EUR,
        "AUD" => Currency.AUD,
        "CAD" => Currency.CAD,
        "VND" => Currency.VND,
        "RUB" => Currency.RUB,
        _ => Currency.USD
    };

    public static string GetSymbol(string code) => ParseCurrency(code).GetSymbol();
}
