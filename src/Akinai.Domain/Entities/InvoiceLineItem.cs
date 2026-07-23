using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class InvoiceLineItem : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public Guid? ProductId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public int Order { get; private set; }

    public Invoice Invoice { get; private set; } = null!;
    public Product? Product { get; private set; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    private InvoiceLineItem() { }

    public static InvoiceLineItem Create(
        Guid invoiceId,
        string description,
        decimal quantity,
        decimal unitPrice,
        string currency,
        int order,
        Guid? productId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Line item description is required");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero");

        return new InvoiceLineItem
        {
            InvoiceId = invoiceId,
            ProductId = productId,
            Description = description,
            Quantity = quantity,
            UnitPrice = new Money(unitPrice, currency),
            Order = order
        };
    }

    public void Update(
        string description,
        decimal quantity,
        decimal unitPrice,
        string currency,
        Guid? productId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Line item description is required");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero");

        Description = description;
        Quantity = quantity;
        UnitPrice = new Money(unitPrice, currency);
        ProductId = productId;
    }

    internal void SetOrder(int order) => Order = order;
}
