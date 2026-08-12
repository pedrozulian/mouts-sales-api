using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Infrastructure.HealthChecks;

public sealed class PendingMigrationsHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public PendingMigrationsHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        return pendingMigrations.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(
                $"Existem migrations pendentes: {string.Join(", ", pendingMigrations)}.");
    }
}
