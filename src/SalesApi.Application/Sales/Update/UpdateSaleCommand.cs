using MediatR;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.Update;

public sealed record UpdateSaleCommand(
    Guid Id,
    DateTime? SaleDate,
    ExternalReferenceRequest Customer,
    ExternalReferenceRequest Branch,
    IReadOnlyCollection<SaleItemChangeRequest> Items) : IRequest<Result<SaleResponse>>;
