using MediatR;
using Microsoft.Extensions.Logging;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Application.Sales.Events;

public sealed class SaleModifiedEventHandler : INotificationHandler<SaleModified>
{
    private readonly ILogger<SaleModifiedEventHandler> _logger;

    public SaleModifiedEventHandler(ILogger<SaleModifiedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SaleModified notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Venda {SaleNumber} ({SaleId}) alterada com sucesso, novo total {TotalAmount}.",
            notification.SaleNumber,
            notification.SaleId,
            notification.TotalAmount);

        return Task.CompletedTask;
    }
}
