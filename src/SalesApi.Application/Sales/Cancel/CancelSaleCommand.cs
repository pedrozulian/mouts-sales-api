using MediatR;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.Cancel;

public sealed record CancelSaleCommand(Guid Id) : IRequest<Result>;
