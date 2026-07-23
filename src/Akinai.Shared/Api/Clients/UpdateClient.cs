using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Clients;

[Route("api/clients/{Id}")]
public class UpdateClient : IPut
{
    [RouteParam]
    public Guid Id { get; init; }

    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? CompanyName { get; init; }

    [BodyParam]
    public string Email { get; init; } = null!;

    [BodyParam]
    public string? Phone { get; init; }

    [BodyParam]
    public decimal DefaultHourlyRate { get; init; }

    [BodyParam]
    public string Currency { get; init; } = "USD";

    [BodyParam]
    public bool IsActive { get; init; }

    [BodyParam]
    public string? Street { get; init; }

    [BodyParam]
    public string? City { get; init; }

    [BodyParam]
    public string? State { get; init; }

    [BodyParam]
    public string? PostalCode { get; init; }

    [BodyParam]
    public string? Country { get; init; }
}
