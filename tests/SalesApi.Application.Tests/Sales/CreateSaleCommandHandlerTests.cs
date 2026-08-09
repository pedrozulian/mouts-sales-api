using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesApi.Application.Common;
using SalesApi.Application.Sales.Create;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Application.Tests.Sales;

public class CreateSaleCommandHandlerTests
{
    private sealed class FakeSaleNumberGenerator : ISaleNumberGenerator
    {
        private readonly string _value;

        public FakeSaleNumberGenerator(string value) => _value = value;

        public Task<string> NextAsync(CancellationToken cancellationToken = default) => Task.FromResult(_value);
    }

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

    private static CreateSaleCommand ValidCommand() => new(
        null,
        new ExternalReferenceRequest(Guid.NewGuid(), "Maria Souza"),
        new ExternalReferenceRequest(Guid.NewGuid(), "Filial Centro"),
        new[]
        {
            new SaleItemRequest(new ExternalReferenceRequest(Guid.NewGuid(), "Teclado Mecânico K68"), 10, 250.00m),
        });

    [Fact]
    public async Task Handle_ComRequisicaoValida_DeveGerarSaleNumberPersistirEDevolverRespostaCompleta()
    {
        await using var context = CreateContext();
        var handler = new CreateSaleCommandHandler(context, new FakeSaleNumberGenerator("V-000123"));

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("V-000123", result.Value!.SaleNumber);
        Assert.Equal(2000.00m, result.Value.TotalAmount);
        Assert.Single(result.Value.Items);
        Assert.Equal(0.20m, result.Value.Items.Single().DiscountPercentage);
        Assert.Equal(1, await context.Sales.CountAsync());
    }

    [Fact]
    public async Task Handle_QuandoSaleCreateFalha_DeveRetornarResultDeFalhaSemPersistirNada()
    {
        await using var context = CreateContext();
        var handler = new CreateSaleCommandHandler(context, new FakeSaleNumberGenerator("V-000124"));

        var command = new CreateSaleCommand(
            null,
            new ExternalReferenceRequest(Guid.NewGuid(), "Maria Souza"),
            new ExternalReferenceRequest(Guid.NewGuid(), "Filial Centro"),
            Array.Empty<SaleItemRequest>());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await context.Sales.CountAsync());
    }
}
