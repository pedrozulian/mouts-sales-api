using Mapster;
using MediatR;
using SalesApi.Application.Common;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Common;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Sales.Create;

public sealed class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Result<SaleResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ISaleNumberGenerator _saleNumberGenerator;

    public CreateSaleCommandHandler(IApplicationDbContext context, ISaleNumberGenerator saleNumberGenerator)
    {
        _context = context;
        _saleNumberGenerator = saleNumberGenerator;
    }

    public async Task<Result<SaleResponse>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var saleNumber = await _saleNumberGenerator.NextAsync(cancellationToken);

        var customer = request.Customer.Adapt<ExternalReference>();
        var branch = request.Branch.Adapt<ExternalReference>();
        var items = request.Items.Adapt<List<SaleItemInput>>();

        var result = Sale.Create(customer, branch, items, saleNumber, request.SaleDate);

        if (!result.IsSuccess)
        {
            return Result<SaleResponse>.Failure(result.Errors);
        }

        _context.Sales.Add(result.Value!);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SaleResponse>.Success(result.Value!.Adapt<SaleResponse>());
    }
}
