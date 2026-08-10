using MediatR;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.Get;

public sealed record GetSaleQuery(Guid Id) : IRequest<Result<SaleResponse>>;
