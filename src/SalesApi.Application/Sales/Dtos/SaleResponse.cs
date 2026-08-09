namespace SalesApi.Application.Sales.Dtos;

public sealed record SaleResponse(
    Guid Id,
    string SaleNumber,
    DateTime SaleDate,
    ExternalReferenceResponse Customer,
    ExternalReferenceResponse Branch,
    decimal TotalAmount,
    bool IsCancelled,
    IReadOnlyCollection<SaleItemResponse> Items);
