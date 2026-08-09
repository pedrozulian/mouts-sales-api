namespace SalesApi.Application.Sales.Dtos;

public sealed record CreateSaleRequest(
    DateTime? SaleDate,
    ExternalReferenceRequest Customer,
    ExternalReferenceRequest Branch,
    IReadOnlyCollection<SaleItemRequest> Items);
