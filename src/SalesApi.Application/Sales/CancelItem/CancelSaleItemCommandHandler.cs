using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApi.Application.Common;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.CancelItem;

public sealed class CancelSaleItemCommandHandler : IRequestHandler<CancelSaleItemCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CancelSaleItemCommandHandler> _logger;

    public CancelSaleItemCommandHandler(IApplicationDbContext context, ILogger<CancelSaleItemCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result> Handle(CancelSaleItemCommand request, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, cancellationToken);

        if (sale is null)
        {
            _logger.LogWarning(
                "Venda {SaleId} não encontrada para cancelamento do item {ItemId}.",
                request.SaleId,
                request.ItemId);

            return Result.Failure(new Notification("id", "Venda não encontrada."));
        }

        var result = sale.CancelItem(request.ItemId);

        if (!result.IsSuccess)
        {
            return Result.Failure(result.Errors);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(
                exception,
                "Conflito de concorrência ao cancelar o item {ItemId} da venda {SaleId} — outra requisição já o alterou.",
                request.ItemId,
                request.SaleId);

            return Result.Failure(new Notification("item", "Item já está cancelado."));
        }

        _logger.LogInformation(
            "Item {ItemId} da venda {SaleNumber} ({SaleId}) cancelado com sucesso.",
            request.ItemId,
            sale.SaleNumber,
            sale.Id);

        return Result.Success();
    }
}
