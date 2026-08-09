namespace SalesApi.Application.Sales.Dtos;

public sealed record SaleItemResponse(
    Guid Id,
    ExternalReferenceResponse Product,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    decimal TotalAmount,
    bool IsCancelled);
