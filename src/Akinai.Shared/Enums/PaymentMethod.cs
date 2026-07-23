namespace Akinai.Shared.Enums;

public enum PaymentMethod
{
    Cash,
    CreditCard,
    DebitCard,
    BankTransfer,
    Other
}

public static class PaymentMethodExtensions
{
    extension(PaymentMethod method)
    {
        public string GetDisplayName() => method switch
        {
            PaymentMethod.Cash => "Cash",
            PaymentMethod.CreditCard => "Credit Card",
            PaymentMethod.DebitCard => "Debit Card",
            PaymentMethod.BankTransfer => "Bank Transfer",
            PaymentMethod.Other => "Other",
            _ => method.ToString()
        };
    }

    public static PaymentMethod Parse(string method) => method switch
    {
        "Cash" => PaymentMethod.Cash,
        "CreditCard" => PaymentMethod.CreditCard,
        "DebitCard" => PaymentMethod.DebitCard,
        "BankTransfer" => PaymentMethod.BankTransfer,
        "Other" => PaymentMethod.Other,
        _ => PaymentMethod.Other
    };
}
