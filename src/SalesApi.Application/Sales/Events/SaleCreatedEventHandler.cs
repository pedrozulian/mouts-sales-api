using MediatR;
using Microsoft.Extensions.Logging;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Application.Sales.Events;

public sealed class SaleCreatedEventHandler : INotificationHandler<SaleCreated>
{
    private readonly ILogger<SaleCreatedEventHandler> _logger;

    public SaleCreatedEventHandler(ILogger<SaleCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SaleCreated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Venda {SaleNumber} ({SaleId}) registrada com sucesso para o cliente {CustomerId}, total {TotalAmount}.",
            notification.SaleNumber,
            notification.SaleId,
            notification.CustomerId,
            notification.TotalAmount);

        return Task.CompletedTask;
    }
}
