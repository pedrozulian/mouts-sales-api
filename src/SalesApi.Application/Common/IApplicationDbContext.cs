using Microsoft.EntityFrameworkCore;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Common;

public interface IApplicationDbContext
{
    DbSet<Sale> Sales { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
