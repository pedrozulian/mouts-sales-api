using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesApi.Application.Sales.Get;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Application.Tests.Sales;

public class GetSaleQueryHandlerTests
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    private sealed class SpyPublisher : IPublisher
    {
        public int PublishCount { get; private set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            PublishCount++;

            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            PublishCount++;

            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext(string databaseName, IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options, publisher ?? new NoOpPublisher());
    }

    private static Sale CreateValidSale(string saleNumber)
    {
        var items = new[]
        {
            new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68"), 10, 250.00m),
        };

        var result = Sale.Create(
            new ExternalReference(Guid.NewGuid(), "Maria Souza"),
            new ExternalReference(Guid.NewGuid(), "Filial Centro"),
            items,
            saleNumber);

        return result.Value!;
    }

    private static async Task<Sale> SeedSaleAsync(string databaseName, Sale sale)
    {
        await using var context = CreateContext(databaseName);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        return sale;
    }

    [Fact]
    public async Task Handle_ComVendaExistente_DeveRetornarRespostaCompletaComClienteFilialEItensDenormalizados()
    {
        var databaseName = Guid.NewGuid().ToString();
        var sale = await SeedSaleAsync(databaseName, CreateValidSale("V-000001"));

        await using var context = CreateContext(databaseName);
        var handler = new GetSaleQueryHandler(context, NullLogger<GetSaleQueryHandler>.Instance);

        var result = await handler.Handle(new GetSaleQuery(sale.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.Equal(sale.Id, response.Id);
        Assert.Equal(sale.SaleNumber, response.SaleNumber);
        Assert.Equal(sale.Customer.Id, response.Customer.Id);
        Assert.Equal(sale.Customer.Name, response.Customer.Name);
        Assert.Equal(sale.Branch.Id, response.Branch.Id);
        Assert.Equal(sale.Branch.Name, response.Branch.Name);
        Assert.Equal(sale.TotalAmount, response.TotalAmount);
        Assert.False(response.IsCancelled);

        var domainItem = sale.Items.Single();
        var item = Assert.Single(response.Items);
        Assert.Equal(domainItem.Product.Id, item.Product.Id);
        Assert.Equal(domainItem.Product.Name, item.Product.Name);
        Assert.Equal(domainItem.DiscountPercentage, item.DiscountPercentage);
        Assert.Equal(domainItem.DiscountAmount, item.DiscountAmount);
        Assert.Equal(domainItem.TotalAmount, item.TotalAmount);
        Assert.False(item.IsCancelled);
    }

    [Fact]
    public async Task Handle_ComVendaExistente_NaoDeveAlterarEstadoNemPublicarEventos()
    {
        var databaseName = Guid.NewGuid().ToString();
        var sale = await SeedSaleAsync(databaseName, CreateValidSale("V-000002"));

        var spyPublisher = new SpyPublisher();
        await using var context = CreateContext(databaseName, spyPublisher);
        var handler = new GetSaleQueryHandler(context, NullLogger<GetSaleQueryHandler>.Instance);

        await handler.Handle(new GetSaleQuery(sale.Id), CancellationToken.None);

        Assert.Equal(0, spyPublisher.PublishCount);

        await using var verificationContext = CreateContext(databaseName);
        var persisted = await verificationContext.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .SingleAsync(s => s.Id == sale.Id);

        Assert.Equal(sale.TotalAmount, persisted.TotalAmount);
        Assert.Equal(sale.IsCancelled, persisted.IsCancelled);
        Assert.Equal(sale.Items.Single().TotalAmount, persisted.Items.Single().TotalAmount);
        Assert.Equal(sale.Items.Single().IsCancelled, persisted.Items.Single().IsCancelled);
    }

    [Fact]
    public async Task Handle_ComItemCancelado_DeveRetornarItemCanceladoVisivelETotalRefletindoApenasItensAtivos()
    {
        var databaseName = Guid.NewGuid().ToString();
        var items = new[]
        {
            new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68"), 10, 250.00m),
            new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Mousepad XL"), 2, 49.90m),
        };
        var sale = Sale.Create(
            new ExternalReference(Guid.NewGuid(), "Maria Souza"),
            new ExternalReference(Guid.NewGuid(), "Filial Centro"),
            items,
            "V-000003").Value!;
        var cancelledItem = sale.Items.First();
        var activeItem = sale.Items.Last();

        await using (var context = CreateContext(databaseName))
        {
            context.Sales.Add(sale);
            await context.SaveChangesAsync();

            context.Entry(cancelledItem).Property(nameof(SaleItem.IsCancelled)).CurrentValue = true;
            context.Entry(sale).Property(nameof(Sale.TotalAmount)).CurrentValue = activeItem.TotalAmount;
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateContext(databaseName);
        var handler = new GetSaleQueryHandler(readContext, NullLogger<GetSaleQueryHandler>.Instance);

        var result = await handler.Handle(new GetSaleQuery(sale.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.False(response.IsCancelled);
        Assert.Equal(activeItem.TotalAmount, response.TotalAmount);
        Assert.Equal(2, response.Items.Count);
        Assert.True(response.Items.Single(i => i.Id == cancelledItem.Id).IsCancelled);
        Assert.False(response.Items.Single(i => i.Id == activeItem.Id).IsCancelled);
    }

    [Fact]
    public async Task Handle_ComVendaCanceladaIntegralmente_DeveRetornarIsCancelledVerdadeiroETotalZerado()
    {
        var databaseName = Guid.NewGuid().ToString();
        var sale = CreateValidSale("V-000004");
        var item = sale.Items.Single();

        await using (var context = CreateContext(databaseName))
        {
            context.Sales.Add(sale);
            await context.SaveChangesAsync();

            context.Entry(item).Property(nameof(SaleItem.IsCancelled)).CurrentValue = true;
            context.Entry(sale).Property(nameof(Sale.IsCancelled)).CurrentValue = true;
            context.Entry(sale).Property(nameof(Sale.TotalAmount)).CurrentValue = 0m;
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateContext(databaseName);
        var handler = new GetSaleQueryHandler(readContext, NullLogger<GetSaleQueryHandler>.Instance);

        var result = await handler.Handle(new GetSaleQuery(sale.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        Assert.True(response.IsCancelled);
        Assert.Equal(0m, response.TotalAmount);
        var responseItem = Assert.Single(response.Items);
        Assert.True(responseItem.IsCancelled);
    }

    [Fact]
    public async Task Handle_ComIdInexistente_DeveRetornarResultDeFalhaComNotificationId()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var handler = new GetSaleQueryHandler(context, NullLogger<GetSaleQueryHandler>.Instance);

        var result = await handler.Handle(new GetSaleQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Key == "id");
    }
}
