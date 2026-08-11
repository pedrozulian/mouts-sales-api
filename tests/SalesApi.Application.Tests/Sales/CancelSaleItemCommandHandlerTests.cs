using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesApi.Application.Sales.CancelItem;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Application.Tests.Sales;

public class CancelSaleItemCommandHandlerTests
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

    private static async Task<Sale> SeedSaleAsync(AppDbContext context, int itemCount = 2)
    {
        var items = itemCount == 1
            ? new[] { new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68"), 2, 250.00m) }
            : new[]
            {
                new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68"), 1, 250.00m),
                new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Mousepad XL"), 2, 49.90m),
            };

        var result = Sale.Create(
            new ExternalReference(Guid.NewGuid(), "Maria Souza"),
            new ExternalReference(Guid.NewGuid(), "Filial Centro"),
            items,
            "V-000400",
            DateTime.UtcNow);

        var sale = result.Value!;
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        sale.ClearDomainEvents();

        return sale;
    }

    [Fact]
    public async Task Handle_ComVendaEItemAtivosExistentes_DeveCancelarItemPersistirEDevolverSucesso()
    {
        await using var context = CreateContext();
        var sale = await SeedSaleAsync(context);
        var itemId = sale.Items.First().Id;

        var handler = new CancelSaleItemCommandHandler(context, NullLogger<CancelSaleItemCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleItemCommand(sale.Id, itemId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persisted = await context.Sales.Include(s => s.Items).SingleAsync(s => s.Id == sale.Id);
        Assert.True(persisted.Items.Single(i => i.Id == itemId).IsCancelled);
        Assert.False(persisted.IsCancelled);
    }

    [Fact]
    public async Task Handle_ComSaleIdInexistente_DeveRetornarFailureComChaveId()
    {
        await using var context = CreateContext();
        var handler = new CancelSaleItemCommandHandler(context, NullLogger<CancelSaleItemCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleItemCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "id");
    }

    [Fact]
    public async Task Handle_ComItemIdInexistenteNaVenda_DeveRetornarFailureComChaveItemId()
    {
        await using var context = CreateContext();
        var sale = await SeedSaleAsync(context);

        var handler = new CancelSaleItemCommandHandler(context, NullLogger<CancelSaleItemCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleItemCommand(sale.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "itemId");
    }

    [Fact]
    public async Task Handle_ComVendaJaCancelada_DeveRetornarFailureComChaveSaleSemPersistirNada()
    {
        await using var context = CreateContext();
        var sale = await SeedSaleAsync(context, itemCount: 1);
        var itemId = sale.Items.Single().Id;
        sale.Cancel();
        await context.SaveChangesAsync();

        var handler = new CancelSaleItemCommandHandler(context, NullLogger<CancelSaleItemCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleItemCommand(sale.Id, itemId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "sale");
    }

    [Fact]
    public async Task Handle_ComItemJaCancelado_DeveRetornarFailureComChaveItem()
    {
        await using var context = CreateContext();
        var sale = await SeedSaleAsync(context);
        var itemId = sale.Items.First().Id;
        sale.CancelItem(itemId);
        await context.SaveChangesAsync();

        var handler = new CancelSaleItemCommandHandler(context, NullLogger<CancelSaleItemCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleItemCommand(sale.Id, itemId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "item");
    }

    [Fact]
    public async Task Handle_ComConflitoDeConcorrencia_DeveRetornarFailureComChaveItem()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;

        await using var seedContext = new AppDbContext(options, new NoOpPublisher());
        var seedResult = Sale.Create(
            new ExternalReference(Guid.NewGuid(), "Maria Souza"),
            new ExternalReference(Guid.NewGuid(), "Filial Centro"),
            new[] { new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Produto"), 2, 250.00m) },
            "V-000401",
            DateTime.UtcNow);
        var sale = seedResult.Value!;
        seedContext.Sales.Add(sale);
        await seedContext.SaveChangesAsync();
        var itemId = sale.Items.Single().Id;

        // Mesma técnica de CancelSaleCommandHandlerTests (006): o EF Core InMemory não gera um
        // novo valor de xmin sozinho a cada SaveChanges (ao contrário do PostgreSQL real), então
        // o OriginalValue do token de concorrência é forçado manualmente para reproduzir o
        // conflito que o xmin real detectaria quando outra requisição já cancelou este item
        // (ver research.md, seção 4).
        await using var context = new AppDbContext(options, new NoOpPublisher());
        var tracked = await context.Sales.Include(s => s.Items).SingleAsync(s => s.Id == sale.Id);
        context.Entry(tracked).Property("xmin").OriginalValue = (uint)999;

        var handler = new CancelSaleItemCommandHandler(context, NullLogger<CancelSaleItemCommandHandler>.Instance);

        var result = await handler.Handle(new CancelSaleItemCommand(sale.Id, itemId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "item");
    }
}
