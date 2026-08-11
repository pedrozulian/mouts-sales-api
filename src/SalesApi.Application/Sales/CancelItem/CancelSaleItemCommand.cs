using MediatR;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.CancelItem;

public sealed record CancelSaleItemCommand(Guid SaleId, Guid ItemId) : IRequest<Result>;
