using MediatR;
using Microsoft.Extensions.Logging;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Application.Sales.Events;

public sealed class SaleCancelledEventHandler : INotificationHandler<SaleCancelled>
{
    private readonly ILogger<SaleCancelledEventHandler> _logger;

    public SaleCancelledEventHandler(ILogger<SaleCancelledEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SaleCancelled notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Venda {SaleNumber} ({SaleId}) cancelada com sucesso.",
            notification.SaleNumber,
            notification.SaleId);

        return Task.CompletedTask;
    }
}
