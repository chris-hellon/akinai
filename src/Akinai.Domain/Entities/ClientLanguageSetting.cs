using Akinai.Domain.Common;
using Akinai.Domain.Enums;

namespace Akinai.Domain.Entities;

public class ClientLanguageSetting : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public SupportedLanguage PreferredLanguage { get; private set; }
    public bool UseForInvoices { get; private set; } = true;
    public bool UseForEstimates { get; private set; } = true;

    public Client Client { get; private set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private ClientLanguageSetting() { }

    public static ClientLanguageSetting Create(
        string userId,
        Guid clientId,
        SupportedLanguage preferredLanguage,
        bool useForInvoices = true,
        bool useForEstimates = true)
    {
        return new ClientLanguageSetting
        {
            UserId = userId,
            ClientId = clientId,
            PreferredLanguage = preferredLanguage,
            UseForInvoices = useForInvoices,
            UseForEstimates = useForEstimates
        };
    }

    public void Update(
        SupportedLanguage preferredLanguage,
        bool useForInvoices,
        bool useForEstimates)
    {
        PreferredLanguage = preferredLanguage;
        UseForInvoices = useForInvoices;
        UseForEstimates = useForEstimates;
    }
}
