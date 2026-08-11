using SalesApi.Domain.Common;

namespace SalesApi.Domain.Sales.Events;

public sealed class ItemCancelled : DomainEvent
{
    public Guid SaleId { get; }

    public Guid SaleItemId { get; }

    public Guid ProductId { get; }

    public int Quantity { get; }

    public ItemCancelled(Guid saleId, Guid saleItemId, Guid productId, int quantity)
    {
        SaleId = saleId;
        SaleItemId = saleItemId;
        ProductId = productId;
        Quantity = quantity;
    }
}
