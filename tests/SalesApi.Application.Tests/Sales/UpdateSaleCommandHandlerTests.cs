using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Application.Sales.Update;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Application.Tests.Sales;

public class UpdateSaleCommandHandlerTests
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

    private static async Task<Sale> SeedSaleAsync(AppDbContext context, ExternalReference product, int quantity = 2, decimal unitPrice = 250.00m)
    {
        var result = Sale.Create(
            new ExternalReference(Guid.NewGuid(), "Maria Souza"),
            new ExternalReference(Guid.NewGuid(), "Filial Centro"),
            new[] { new SaleItemInput(product, quantity, unitPrice) },
            "V-000200",
            DateTime.UtcNow);

        var sale = result.Value!;
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        sale.ClearDomainEvents();

        return sale;
    }

    [Fact]
    public async Task Handle_ComVendaExistenteEComandoValido_DeveReconciliarPersistirEDevolverRespostaCompleta()
    {
        await using var context = CreateContext();
        var product = new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68");
        var sale = await SeedSaleAsync(context, product);
        var itemId = sale.Items.Single().Id;

        var handler = new UpdateSaleCommandHandler(context, NullLogger<UpdateSaleCommandHandler>.Instance);
        var command = new UpdateSaleCommand(
            sale.Id,
            sale.SaleDate,
            new ExternalReferenceRequest(sale.Customer.Id, sale.Customer.Name),
            new ExternalReferenceRequest(sale.Branch.Id, sale.Branch.Name),
            new[] { new SaleItemChangeRequest(itemId, new ExternalReferenceRequest(product.Id, product.Name), 12, 250.00m) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2400.00m, result.Value!.TotalAmount);
        Assert.Single(result.Value.Items);
        Assert.Equal(12, result.Value.Items.Single().Quantity);

        var persisted = await context.Sales.Include(s => s.Items).SingleAsync(s => s.Id == sale.Id);
        Assert.Equal(2400.00m, persisted.TotalAmount);
    }

    [Fact]
    public async Task Handle_ComIdInexistente_DeveRetornarFailureComChaveId()
    {
        await using var context = CreateContext();
        var handler = new UpdateSaleCommandHandler(context, NullLogger<UpdateSaleCommandHandler>.Instance);
        var command = new UpdateSaleCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            new ExternalReferenceRequest(Guid.NewGuid(), "Maria Souza"),
            new ExternalReferenceRequest(Guid.NewGuid(), "Filial Centro"),
            new[]
            {
                new SaleItemChangeRequest(null, new ExternalReferenceRequest(Guid.NewGuid(), "Produto"), 1, 10.00m),
            });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "id");
    }

    [Fact]
    public async Task Handle_ComConflitoDeConcorrencia_DeveRetornarFailureComChaveSale()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;

        await using var seedContext = new AppDbContext(options, new NoOpPublisher());
        var product = new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68");
        var sale = await SeedSaleAsync(seedContext, product);
        var itemId = sale.Items.Single().Id;

        // Simula uma segunda requisição que carregou a mesma venda antes de outra requisição
        // concorrente (ex.: um cancelamento) já ter persistido sua mudança — o EF Core InMemory
        // não gera um novo valor de xmin sozinho a cada SaveChanges (ao contrário do PostgreSQL
        // real), então o OriginalValue do token de concorrência é forçado manualmente para
        // reproduzir o conflito que o xmin real detectaria (ver research.md, seção 3).
        await using var context = new AppDbContext(options, new NoOpPublisher());
        var tracked = await context.Sales.Include(s => s.Items).SingleAsync(s => s.Id == sale.Id);
        context.Entry(tracked).Property("xmin").OriginalValue = (uint)999;

        var handler = new UpdateSaleCommandHandler(context, NullLogger<UpdateSaleCommandHandler>.Instance);
        var command = new UpdateSaleCommand(
            sale.Id,
            sale.SaleDate,
            new ExternalReferenceRequest(sale.Customer.Id, sale.Customer.Name),
            new ExternalReferenceRequest(sale.Branch.Id, sale.Branch.Name),
            new[] { new SaleItemChangeRequest(itemId, new ExternalReferenceRequest(product.Id, product.Name), 12, 250.00m) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "sale");
    }
}
