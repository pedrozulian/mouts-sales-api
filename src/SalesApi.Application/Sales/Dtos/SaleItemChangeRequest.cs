namespace SalesApi.Application.Sales.Dtos;

public sealed record SaleItemChangeRequest(Guid? Id, ExternalReferenceRequest Product, int Quantity, decimal UnitPrice);
