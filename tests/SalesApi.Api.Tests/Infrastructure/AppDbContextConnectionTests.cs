using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Api.Tests.Infrastructure;

public class AppDbContextConnectionTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public AppDbContextConnectionTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    [Fact]
    public async Task AppDbContext_DeveConseguirAbrirConexaoComPostgres()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_factory.ConnectionString)
            .Options;

        await using var dbContext = new AppDbContext(options, new NoOpPublisher());

        var canConnect = await dbContext.Database.CanConnectAsync();

        Assert.True(canConnect);
    }
}
