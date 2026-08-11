namespace SalesApi.Application.Sales.Dtos;

public sealed record UpdateSaleRequest(
    DateTime? SaleDate,
    ExternalReferenceRequest Customer,
    ExternalReferenceRequest Branch,
    IReadOnlyCollection<SaleItemChangeRequest> Items);
