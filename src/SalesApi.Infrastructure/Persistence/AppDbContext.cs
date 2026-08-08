using Microsoft.EntityFrameworkCore;
using SalesApi.Application.Common;

namespace SalesApi.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
