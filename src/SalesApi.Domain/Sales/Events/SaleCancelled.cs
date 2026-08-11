using SalesApi.Domain.Common;

namespace SalesApi.Domain.Sales.Events;

public sealed class SaleCancelled : DomainEvent
{
    public Guid SaleId { get; }

    public string SaleNumber { get; }

    public SaleCancelled(Guid saleId, string saleNumber)
    {
        SaleId = saleId;
        SaleNumber = saleNumber;
    }
}
