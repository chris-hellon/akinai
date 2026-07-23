using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class Product : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public decimal DefaultQuantity { get; private set; } = 1;
    public string? Category { get; private set; }
    public string? Sku { get; private set; }
    public decimal? TaxRate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Product() { }

    public static Product Create(
        string userId,
        string name,
        decimal unitPrice,
        string currency,
        string? description = null,
        decimal defaultQuantity = 1,
        string? category = null,
        string? sku = null,
        decimal? taxRate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required");

        return new Product
        {
            UserId = userId,
            Name = name,
            Description = description,
            UnitPrice = new Money(unitPrice, currency),
            DefaultQuantity = Math.Max(0.01m, defaultQuantity),
            Category = category,
            Sku = sku,
            TaxRate = taxRate
        };
    }

    public void Update(
        string name,
        decimal unitPrice,
        string currency,
        string? description,
        decimal defaultQuantity,
        string? category,
        string? sku,
        decimal? taxRate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required");

        Name = name;
        Description = description;
        UnitPrice = new Money(unitPrice, currency);
        DefaultQuantity = Math.Max(0.01m, defaultQuantity);
        Category = category;
        Sku = sku;
        TaxRate = taxRate;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
