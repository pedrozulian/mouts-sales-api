using MediatR;
using SalesApi.Application.Common.Dtos;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.List;

public sealed record ListSalesQuery(
    string? Page,
    string? PageSize,
    string? CustomerId,
    string? BranchId,
    string? IsCancelled) : IRequest<Result<PagedResult<SaleSummaryResponse>>>;
