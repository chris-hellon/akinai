using Akinai.Domain.Common;
using Akinai.Domain.Exceptions;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class RecurringInvoiceLineItem : BaseEntity
{
    public Guid RecurringInvoiceId { get; private set; }
    public Guid? ProductId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public int Order { get; private set; }

    public RecurringInvoice RecurringInvoice { get; private set; } = null!;
    public Product? Product { get; private set; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    private RecurringInvoiceLineItem() { }

    public static RecurringInvoiceLineItem Create(
        Guid recurringInvoiceId,
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

        return new RecurringInvoiceLineItem
        {
            RecurringInvoiceId = recurringInvoiceId,
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
