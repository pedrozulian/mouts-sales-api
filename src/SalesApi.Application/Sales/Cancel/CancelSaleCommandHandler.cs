using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApi.Application.Common;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.Cancel;

public sealed class CancelSaleCommandHandler : IRequestHandler<CancelSaleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CancelSaleCommandHandler> _logger;

    public CancelSaleCommandHandler(IApplicationDbContext context, ILogger<CancelSaleCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result> Handle(CancelSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (sale is null)
        {
            _logger.LogWarning("Venda {SaleId} não encontrada para cancelamento.", request.Id);

            return Result.Failure(new Notification("id", "Venda não encontrada."));
        }

        var result = sale.Cancel();

        if (!result.IsSuccess)
        {
            return Result.Failure(result.Errors);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "Conflito de concorrência ao cancelar a venda {SaleId} — outra requisição já a cancelou.",
                request.Id);

            return Result.Failure(new Notification("sale", "Venda já está cancelada."));
        }

        _logger.LogInformation("Venda {SaleNumber} ({SaleId}) cancelada com sucesso.", sale.SaleNumber, sale.Id);

        return Result.Success();
    }
}
