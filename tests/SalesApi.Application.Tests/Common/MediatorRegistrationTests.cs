using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Application.Common;
using SalesApi.Application.Sales.Get;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Application.Tests.Common;

public class MediatorRegistrationTests
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    [Fact]
    public async Task GetSaleQuery_DeveSerDespachadaViaIMediatorEResolverOHandlerCorreto()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPublisher>(new NoOpPublisher());
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddApplication();
        await using var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var response = await mediator.Send(new GetSaleQuery(Guid.NewGuid()));

        // Não existe venda com esse Id — o que importa aqui é que o MediatR resolveu e despachou
        // para GetSaleQueryHandler via DI, não o resultado de negócio em si.
        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, e => e.Key == "id");
    }
}
