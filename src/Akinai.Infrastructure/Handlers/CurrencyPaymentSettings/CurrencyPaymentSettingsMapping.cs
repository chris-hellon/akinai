using System.Linq.Expressions;
using Akinai.Shared.Dtos.CurrencyPaymentSettings;

using Entity = Akinai.Domain.Entities.CurrencyPaymentSettings;

namespace Akinai.Infrastructure.Handlers.CurrencyPaymentSettings;

/// <summary>
/// The one place a currency's payment details become a DTO.
///
/// There were three of these, written at different times, and they had drifted: the
/// public copy of an invoice stopped mapping at VietQrBankCode, so a UPI, PIX or
/// PromptPay account produced a scan-to-pay code in the owner's own view and nothing at
/// all in the copy the client was actually sent. A field added to the entity and missed
/// in one projection is invisible until someone cannot pay an invoice.
/// </summary>
public static class CurrencyPaymentSettingsMapping
{
    /// <summary>Projection for use inside a query, so EF selects only these columns.</summary>
    public static Expression<Func<Entity, CurrencyPaymentSettingsDto>> Projection { get; } = c =>
        new CurrencyPaymentSettingsDto(
            c.Id,
            c.CurrencyCode,
            c.BankName,
            c.BankAccountName,
            c.BankAccountNumber,
            c.BankSortCode,
            c.BankIban,
            c.BankSwift,
            c.VietQrBankCode,
            c.IntermediaryBankName,
            c.IntermediaryBankSwift,
            c.IntermediaryAccountNumber,
            c.BankAddress,
            c.PaymentPurposeCode,
            c.UpiVpa,
            c.PixKey,
            c.PromptPayId);

    private static readonly Func<Entity, CurrencyPaymentSettingsDto> Compiled = Projection.Compile();

    /// <summary>Maps an entity already in memory.</summary>
    public static CurrencyPaymentSettingsDto ToDto(Entity entity) => Compiled(entity);
}
