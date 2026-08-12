using MediatR;
using Microsoft.Extensions.Logging;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Application.Sales.Events;

public sealed class ItemCancelledEventHandler : INotificationHandler<ItemCancelled>
{
    private readonly ILogger<ItemCancelledEventHandler> _logger;

    public ItemCancelledEventHandler(ILogger<ItemCancelledEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ItemCancelled notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Item {SaleItemId} do produto {ProductId} cancelado na venda {SaleNumber} ({SaleId}), quantidade {Quantity}.",
            notification.SaleItemId,
            notification.ProductId,
            notification.SaleNumber,
            notification.SaleId,
            notification.Quantity);

        return Task.CompletedTask;
    }
}
