using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SalesApi.Infrastructure.HealthChecks;
using SalesApi.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalesApi.Api.Tests.Infrastructure;

// Diferente do resto da suíte de integração (que usa SalesApiFactory, sempre migrado no
// InitializeAsync), este teste precisa de um Postgres explicitamente NÃO migrado para provar que
// o health check detecta schema ausente/desatualizado — por isso sobe seu próprio container.
public sealed class PendingMigrationsHealthCheckTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("salesapi_health_test")
        .WithUsername("salesapi")
        .WithPassword("salesapi")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : MediatR.INotification
            => Task.CompletedTask;
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new AppDbContext(options, new NoOpPublisher());
    }

    [Fact]
    public async Task CheckHealthAsync_ComSchemaNaoMigrado_DeveRetornarUnhealthy()
    {
        await using var context = CreateContext();
        var healthCheck = new PendingMigrationsHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("migrations pendentes", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ComSchemaAtualizado_DeveRetornarHealthy()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var healthCheck = new PendingMigrationsHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
