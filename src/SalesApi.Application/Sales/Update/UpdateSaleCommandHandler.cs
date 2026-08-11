using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApi.Application.Common;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Sales.Update;

public sealed class UpdateSaleCommandHandler : IRequestHandler<UpdateSaleCommand, Result<SaleResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateSaleCommandHandler> _logger;

    public UpdateSaleCommandHandler(IApplicationDbContext context, ILogger<UpdateSaleCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<SaleResponse>> Handle(UpdateSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (sale is null)
        {
            _logger.LogWarning("Venda {SaleId} não encontrada para alteração.", request.Id);

            return Result<SaleResponse>.Failure(new Notification("id", "Venda não encontrada."));
        }

        var customer = request.Customer.Adapt<ExternalReference>();
        var branch = request.Branch.Adapt<ExternalReference>();
        var items = request.Items.Adapt<List<SaleItemChangeInput>>();

        var result = sale.Update(customer, branch, request.SaleDate, items);

        if (!result.IsSuccess)
        {
            return Result<SaleResponse>.Failure(result.Errors);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(
                exception,
                "Conflito de concorrência ao alterar a venda {SaleId} — outra requisição já a cancelou.",
                request.Id);

            return Result<SaleResponse>.Failure(new Notification("sale", "Venda cancelada não pode ser alterada."));
        }

        _logger.LogInformation("Venda {SaleNumber} ({SaleId}) alterada com sucesso.", sale.SaleNumber, sale.Id);

        return Result<SaleResponse>.Success(sale.Adapt<SaleResponse>());
    }
}
