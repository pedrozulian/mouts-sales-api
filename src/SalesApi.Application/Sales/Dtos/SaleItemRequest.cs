namespace SalesApi.Application.Sales.Dtos;

public sealed record SaleItemRequest(ExternalReferenceRequest Product, int Quantity, decimal UnitPrice);
