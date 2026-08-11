using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesApi.Application.Sales.Cancel;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Application.Tests.Sales;

public class CancelSaleCommandHandlerTests
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new NoOpPublisher());
    }

    private static async Task<Sale> SeedSaleAsync(AppDbContext context)
    {
        var result = Sale.Create(
            new ExternalReference(Guid.NewGuid(), "Maria Souza"),
            new ExternalReference(Guid.NewGuid(), "Filial Centro"),
            new[] { new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68"), 2, 250.00m) },
            "V-000300",
            DateTime.UtcNow);

        var sale = result.Value!;
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        sale.ClearDomainEvents();

        return sale;
    }

    [Fact]
    public async Task Handle_ComVendaAtivaExistente_DeveCancelarPersistirEDevolverSucesso()
    {
        await using var context = CreateContext();
        var sale = await SeedSaleAsync(context);

        var handler = new CancelSaleCommandHandler(context, NullLogger<CancelSaleCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleCommand(sale.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persisted = await context.Sales.Include(s => s.Items).SingleAsync(s => s.Id == sale.Id);
        Assert.True(persisted.IsCancelled);
        Assert.Equal(0m, persisted.TotalAmount);
        Assert.All(persisted.Items, item => Assert.True(item.IsCancelled));
    }

    [Fact]
    public async Task Handle_ComIdInexistente_DeveRetornarFailureComChaveId()
    {
        await using var context = CreateContext();
        var handler = new CancelSaleCommandHandler(context, NullLogger<CancelSaleCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "id");
    }

    [Fact]
    public async Task Handle_ComVendaJaCancelada_DeveRetornarFailureComChaveSaleSemPersistirNada()
    {
        await using var context = CreateContext();
        var sale = await SeedSaleAsync(context);
        sale.Cancel();
        await context.SaveChangesAsync();

        var handler = new CancelSaleCommandHandler(context, NullLogger<CancelSaleCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleCommand(sale.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "sale");
    }
}
