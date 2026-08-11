using SalesApi.Domain.Common;

namespace SalesApi.Domain.Sales.Events;

public sealed class SaleModified : DomainEvent
{
    public Guid SaleId { get; }

    public string SaleNumber { get; }

    public decimal TotalAmount { get; }

    public SaleModified(Guid saleId, string saleNumber, decimal totalAmount)
    {
        SaleId = saleId;
        SaleNumber = saleNumber;
        TotalAmount = totalAmount;
    }
}
