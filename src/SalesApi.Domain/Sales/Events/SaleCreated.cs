using SalesApi.Domain.Common;

namespace SalesApi.Domain.Sales.Events;

public sealed class SaleCreated : DomainEvent
{
    public Guid SaleId { get; }

    public string SaleNumber { get; }

    public Guid CustomerId { get; }

    public Guid BranchId { get; }

    public decimal TotalAmount { get; }

    public SaleCreated(Guid saleId, string saleNumber, Guid customerId, Guid branchId, decimal totalAmount)
    {
        SaleId = saleId;
        SaleNumber = saleNumber;
        CustomerId = customerId;
        BranchId = branchId;
        TotalAmount = totalAmount;
    }
}
