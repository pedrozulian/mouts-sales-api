namespace SalesApi.Domain.Sales;

public sealed record SaleItemInput(ExternalReference Product, int Quantity, decimal UnitPrice);
