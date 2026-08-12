using Microsoft.EntityFrameworkCore;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Api.Tests.Infrastructure;

public class SchemaNamingTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public SchemaNamingTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : MediatR.INotification
            => Task.CompletedTask;
    }

    [Fact]
    public async Task Schema_ComColunasRenomeadasParaSnakeCase_DeveSerConsultavelSemDelimitacaoEspecial()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_factory.ConnectionString)
            .Options;

        await using var context = new AppDbContext(options, new NoOpPublisher());
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var salesCommand = connection.CreateCommand();
        salesCommand.CommandText =
            "SELECT id, sale_number, sale_date, total_amount, is_cancelled, created_at, updated_at, " +
            "customer_id, customer_name, branch_id, branch_name FROM sales LIMIT 1";
        await using var salesReader = await salesCommand.ExecuteReaderAsync();
        Assert.Equal(11, salesReader.FieldCount);
        await salesReader.DisposeAsync();

        await using var itemsCommand = connection.CreateCommand();
        itemsCommand.CommandText =
            "SELECT id, sale_id, product_id, product_name, quantity, unit_price, discount_percentage, " +
            "discount_amount, total_amount, is_cancelled FROM sale_items LIMIT 1";
        await using var itemsReader = await itemsCommand.ExecuteReaderAsync();
        Assert.Equal(10, itemsReader.FieldCount);
        await itemsReader.DisposeAsync();
    }
}
