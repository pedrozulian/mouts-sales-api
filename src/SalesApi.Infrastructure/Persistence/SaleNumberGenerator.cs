using Microsoft.EntityFrameworkCore;
using SalesApi.Application.Common;

namespace SalesApi.Infrastructure.Persistence;

public sealed class SaleNumberGenerator : ISaleNumberGenerator
{
    private readonly AppDbContext _context;

    public SaleNumberGenerator(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var sequenceValue = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('sale_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return $"V-{sequenceValue:D6}";
    }
}
