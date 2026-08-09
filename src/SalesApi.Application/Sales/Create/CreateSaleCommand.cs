using MediatR;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.Create;

public sealed record CreateSaleCommand(
    DateTime? SaleDate,
    ExternalReferenceRequest Customer,
    ExternalReferenceRequest Branch,
    IReadOnlyCollection<SaleItemRequest> Items) : IRequest<Result<SaleResponse>>;
