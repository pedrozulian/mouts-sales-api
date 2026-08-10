using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesApi.Application.Sales.List;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Application.Tests.Sales;

public class ListSalesQueryHandlerTests
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }

    private static AppDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options, new NoOpPublisher());
    }

    private static Sale CreateSale(
        string saleNumber,
        DateTime saleDate,
        Guid? customerId = null,
        Guid? branchId = null)
    {
        var items = new[]
        {
            new SaleItemInput(new ExternalReference(Guid.NewGuid(), "Teclado Mecânico K68"), 10, 250.00m),
        };

        var result = Sale.Create(
            new ExternalReference(customerId ?? Guid.NewGuid(), "Maria Souza"),
            new ExternalReference(branchId ?? Guid.NewGuid(), "Filial Centro"),
            items,
            saleNumber,
            saleDate);

        return result.Value!;
    }

    private static async Task SeedAsync(string databaseName, params Sale[] sales)
    {
        await using var context = CreateContext(databaseName);
        context.Sales.AddRange(sales);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_SemParametros_DeveAplicarPaginaEPageSizePadraoEOrdenarPorDataDecrescenteComDesempatePorId()
    {
        var databaseName = Guid.NewGuid().ToString();
        var sameDate = new DateTime(2026, 8, 9, 14, 30, 0, DateTimeKind.Utc);

        var older = CreateSale("V-000001", sameDate.AddDays(-1));
        var tieA = CreateSale("V-000002", sameDate);
        var tieB = CreateSale("V-000003", sameDate);
        await SeedAsync(databaseName, older, tieA, tieB);

        var expectedTieOrder = new[] { tieA, tieB }.OrderByDescending(s => s.Id).Select(s => s.Id).ToArray();

        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(new ListSalesQuery(null, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value!;
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(3, page.Items.Count);

        var ids = page.Items.Select(i => i.Id).ToArray();
        Assert.Equal(expectedTieOrder[0], ids[0]);
        Assert.Equal(expectedTieOrder[1], ids[1]);
        Assert.Equal(older.Id, ids[2]);
    }

    [Fact]
    public async Task Handle_ComPaginaEPageSizeExplicitos_DeveRetornarFatiaEsperadaComMetadadosCorretos()
    {
        var databaseName = Guid.NewGuid().ToString();
        var baseDate = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);

        var sales = Enumerable.Range(0, 5)
            .Select(i => CreateSale($"V-{i:000000}", baseDate.AddMinutes(i)))
            .ToArray();
        await SeedAsync(databaseName, sales);

        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(new ListSalesQuery("2", "2", null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value!;
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);

        // Ordenado por SaleDate decrescente: [4,3,2,1,0]; página 2 (pageSize 2) => índices 2,3 => sales[2], sales[1]
        var ids = page.Items.Select(i => i.Id).ToArray();
        Assert.Equal(sales[2].Id, ids[0]);
        Assert.Equal(sales[1].Id, ids[1]);
    }

    [Fact]
    public async Task Handle_ComFiltroCustomerId_DeveRetornarSomenteVendasDaqueleCliente()
    {
        var databaseName = Guid.NewGuid().ToString();
        var baseDate = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);
        var targetCustomerId = Guid.NewGuid();

        var matching = CreateSale("V-100001", baseDate, customerId: targetCustomerId);
        var other = CreateSale("V-100002", baseDate.AddMinutes(1));
        await SeedAsync(databaseName, matching, other);

        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(
            new ListSalesQuery(null, null, targetCustomerId.ToString(), null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value!;
        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal(matching.Id, item.Id);
    }

    [Fact]
    public async Task Handle_ComFiltroBranchId_DeveRetornarSomenteVendasDaquelaFilial()
    {
        var databaseName = Guid.NewGuid().ToString();
        var baseDate = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);
        var targetBranchId = Guid.NewGuid();

        var matching = CreateSale("V-100003", baseDate, branchId: targetBranchId);
        var other = CreateSale("V-100004", baseDate.AddMinutes(1));
        await SeedAsync(databaseName, matching, other);

        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(
            new ListSalesQuery(null, null, null, targetBranchId.ToString(), null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value!;
        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal(matching.Id, item.Id);
    }

    [Fact]
    public async Task Handle_ComFiltroIsCancelled_DeveRetornarSomenteVendasComASituacaoCorrespondenteEAmbasQuandoAusente()
    {
        var databaseName = Guid.NewGuid().ToString();
        var baseDate = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);
        var active = CreateSale("V-100005", baseDate);
        var cancelled = CreateSale("V-100006", baseDate.AddMinutes(1));

        await using (var context = CreateContext(databaseName))
        {
            context.Sales.AddRange(active, cancelled);
            await context.SaveChangesAsync();

            context.Entry(cancelled).Property(nameof(Sale.IsCancelled)).CurrentValue = true;
            context.Entry(cancelled).Property(nameof(Sale.TotalAmount)).CurrentValue = 0m;
            await context.SaveChangesAsync();
        }

        await using var context1 = CreateContext(databaseName);
        var handlerAusente = new ListSalesQueryHandler(context1, NullLogger<ListSalesQueryHandler>.Instance);
        var resultAusente = await handlerAusente.Handle(new ListSalesQuery(null, null, null, null, null), CancellationToken.None);
        Assert.True(resultAusente.IsSuccess);
        Assert.Equal(2, resultAusente.Value!.TotalCount);

        await using var context2 = CreateContext(databaseName);
        var handlerAtivas = new ListSalesQueryHandler(context2, NullLogger<ListSalesQueryHandler>.Instance);
        var resultAtivas = await handlerAtivas.Handle(new ListSalesQuery(null, null, null, null, "false"), CancellationToken.None);
        Assert.True(resultAtivas.IsSuccess);
        var itemAtiva = Assert.Single(resultAtivas.Value!.Items);
        Assert.Equal(active.Id, itemAtiva.Id);

        await using var context3 = CreateContext(databaseName);
        var handlerCanceladas = new ListSalesQueryHandler(context3, NullLogger<ListSalesQueryHandler>.Instance);
        var resultCanceladas = await handlerCanceladas.Handle(new ListSalesQuery(null, null, null, null, "true"), CancellationToken.None);
        Assert.True(resultCanceladas.IsSuccess);
        var itemCancelada = Assert.Single(resultCanceladas.Value!.Items);
        Assert.Equal(cancelled.Id, itemCancelada.Id);
    }

    [Fact]
    public async Task Handle_ComFiltrosCombinados_DeveRetornarSomenteVendasQueAtendemATodosOsFiltros()
    {
        var databaseName = Guid.NewGuid().ToString();
        var baseDate = new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var matching = CreateSale("V-100007", baseDate, customerId, branchId);
        var wrongBranch = CreateSale("V-100008", baseDate.AddMinutes(1), customerId);
        var wrongCustomer = CreateSale("V-100009", baseDate.AddMinutes(2), branchId: branchId);
        await SeedAsync(databaseName, matching, wrongBranch, wrongCustomer);

        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(
            new ListSalesQuery(null, null, customerId.ToString(), branchId.ToString(), null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(matching.Id, item.Id);
    }

    [Fact]
    public async Task Handle_ComFiltroSemCorrespondencia_DeveRetornarListaVaziaComTotalCountETotalPagesZero()
    {
        var databaseName = Guid.NewGuid().ToString();
        await SeedAsync(databaseName, CreateSale("V-200001", new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc)));

        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(
            new ListSalesQuery(null, null, Guid.NewGuid().ToString(), null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value!;
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task Handle_ComPaginaAlemDoTotal_DeveRetornarListaVaziaComSucesso()
    {
        var databaseName = Guid.NewGuid().ToString();
        await SeedAsync(databaseName, CreateSale("V-200002", new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc)));

        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(new ListSalesQuery("999", null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var page = result.Value!;
        Assert.Empty(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(999, page.Page);
    }

    [Theory]
    [InlineData("0", "page")]
    [InlineData("-1", "page")]
    public async Task Handle_ComPageMenorQueUm_DeveRetornarFalhaComNotificationPage(string rawPage, string expectedKey)
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(new ListSalesQuery(rawPage, null, null, null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == expectedKey);
    }

    [Fact]
    public async Task Handle_ComPageSizeMenorQueUm_DeveRetornarFalhaComNotificationPageSize()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(new ListSalesQuery(null, "0", null, null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "pageSize");
    }

    [Fact]
    public async Task Handle_ComPageSizeAcimaDoLimite_DeveRetornarFalhaComNotificationPageSize()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(new ListSalesQuery(null, "101", null, null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "pageSize");
    }

    [Fact]
    public async Task Handle_ComParametrosDeFiltroEmFormatoInvalido_DeveRetornarFalhaComUmaNotificationPorParametroAcumuladas()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var handler = new ListSalesQueryHandler(context, NullLogger<ListSalesQueryHandler>.Instance);

        var result = await handler.Handle(
            new ListSalesQuery(null, null, "nao-e-um-guid", "tambem-invalido", "talvez"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "customerId");
        Assert.Contains(result.Errors, e => e.Key == "branchId");
        Assert.Contains(result.Errors, e => e.Key == "isCancelled");
        Assert.Equal(3, result.Errors.Count);
    }
}
