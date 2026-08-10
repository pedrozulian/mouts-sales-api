using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApi.Application.Common;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;

namespace SalesApi.Application.Sales.Get;

public sealed class GetSaleQueryHandler : IRequestHandler<GetSaleQuery, Result<SaleResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetSaleQueryHandler> _logger;

    public GetSaleQueryHandler(IApplicationDbContext context, ILogger<GetSaleQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<SaleResponse>> Handle(GetSaleQuery request, CancellationToken cancellationToken)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (sale is null)
        {
            _logger.LogWarning("Venda {SaleId} não encontrada.", request.Id);

            return Result<SaleResponse>.Failure(new Notification("id", "Venda não encontrada."));
        }

        _logger.LogInformation("Venda {SaleNumber} ({SaleId}) encontrada.", sale.SaleNumber, sale.Id);

        return Result<SaleResponse>.Success(sale.Adapt<SaleResponse>());
    }
}
