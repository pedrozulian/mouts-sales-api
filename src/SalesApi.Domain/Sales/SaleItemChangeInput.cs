namespace SalesApi.Domain.Sales;

public sealed record SaleItemChangeInput(Guid? Id, ExternalReference Product, int Quantity, decimal UnitPrice);
