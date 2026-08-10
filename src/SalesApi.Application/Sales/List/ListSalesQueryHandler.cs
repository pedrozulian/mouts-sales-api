using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesApi.Application.Common;
using SalesApi.Application.Common.Dtos;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Sales.List;

public sealed class ListSalesQueryHandler : IRequestHandler<ListSalesQuery, Result<PagedResult<SaleSummaryResponse>>>
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _context;
    private readonly ILogger<ListSalesQueryHandler> _logger;

    public ListSalesQueryHandler(IApplicationDbContext context, ILogger<ListSalesQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<PagedResult<SaleSummaryResponse>>> Handle(ListSalesQuery request, CancellationToken cancellationToken)
    {
        var errors = new List<Notification>();

        var page = ParsePage(request.Page, errors);
        var pageSize = ParsePageSize(request.PageSize, errors);
        var customerId = ParseGuid(request.CustomerId, "customerId", "Identificador de cliente em formato inválido.", errors);
        var branchId = ParseGuid(request.BranchId, "branchId", "Identificador de filial em formato inválido.", errors);
        var isCancelled = ParseIsCancelled(request.IsCancelled, errors);

        if (errors.Count > 0)
        {
            return Result<PagedResult<SaleSummaryResponse>>.Failure(errors);
        }

        IQueryable<Sale> query = _context.Sales.AsNoTracking();

        if (customerId.HasValue)
        {
            query = query.Where(s => s.Customer.Id == customerId.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(s => s.Branch.Id == branchId.Value);
        }

        if (isCancelled.HasValue)
        {
            query = query.Where(s => s.IsCancelled == isCancelled.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectToType<SaleSummaryResponse>()
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listagem de vendas: {TotalCount} venda(s) encontradas para page={Page}, pageSize={PageSize}.",
            totalCount,
            page,
            pageSize);

        var pagedResult = PagedResult<SaleSummaryResponse>.Create(items, page, pageSize, totalCount);

        return Result<PagedResult<SaleSummaryResponse>>.Success(pagedResult);
    }

    private static int ParsePage(string? rawPage, List<Notification> errors)
    {
        if (string.IsNullOrWhiteSpace(rawPage))
        {
            return DefaultPage;
        }

        if (!int.TryParse(rawPage, out var page) || page < 1)
        {
            errors.Add(new Notification("page", "A página deve ser um número inteiro maior ou igual a 1."));

            return DefaultPage;
        }

        return page;
    }

    private static int ParsePageSize(string? rawPageSize, List<Notification> errors)
    {
        if (string.IsNullOrWhiteSpace(rawPageSize))
        {
            return DefaultPageSize;
        }

        if (!int.TryParse(rawPageSize, out var pageSize) || pageSize < 1 || pageSize > MaxPageSize)
        {
            errors.Add(new Notification("pageSize", $"O tamanho de página deve ser um número inteiro entre 1 e {MaxPageSize}."));

            return DefaultPageSize;
        }

        return pageSize;
    }

    private static Guid? ParseGuid(string? rawValue, string key, string message, List<Notification> errors)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (!Guid.TryParse(rawValue, out var value))
        {
            errors.Add(new Notification(key, message));

            return null;
        }

        return value;
    }

    private static bool? ParseIsCancelled(string? rawValue, List<Notification> errors)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (!bool.TryParse(rawValue, out var value))
        {
            errors.Add(new Notification("isCancelled", "O parâmetro isCancelled deve ser \"true\" ou \"false\"."));

            return null;
        }

        return value;
    }
}
