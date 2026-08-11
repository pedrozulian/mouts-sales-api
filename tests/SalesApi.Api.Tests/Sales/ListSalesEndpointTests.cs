using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Api.Tests.Infrastructure;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Api.Tests.Sales;

public class ListSalesEndpointTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public ListSalesEndpointTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private static object Payload(DateTime? saleDate = null, Guid? customerId = null, Guid? branchId = null)
    {
        return new
        {
            saleDate,
            customer = new { id = customerId ?? Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = branchId ?? Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" },
                    quantity = 10,
                    unitPrice = 250.00m,
                },
            },
        };
    }

    private static async Task<SaleResponse> CreateSaleAsync(HttpClient client, object payload)
    {
        var createResponse = await client.PostAsJsonAsync("/api/sales", payload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(created);

        return created!;
    }

    private static int IndexOf(SaleSummaryResponse[] items, Guid id) => Array.FindIndex(items, i => i.Id == id);

    private async Task CancelSaleAsync(Guid saleId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sale = await context.Sales.Include(s => s.Items).SingleAsync(s => s.Id == saleId);

        foreach (var item in sale.Items)
        {
            context.Entry(item).Property(nameof(SaleItem.IsCancelled)).CurrentValue = true;
        }

        context.Entry(sale).Property(nameof(Sale.IsCancelled)).CurrentValue = true;
        context.Entry(sale).Property(nameof(Sale.TotalAmount)).CurrentValue = 0m;

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ListSales_SemParametros_DeveRetornar200ComEnvelopePaginadoOrdenadoPorDataDecrescenteSemItensAninhados()
    {
        using var client = _factory.CreateClient();
        var newer = await CreateSaleAsync(client, Payload(new DateTime(2099, 1, 1, 10, 0, 0, DateTimeKind.Utc)));
        var older = await CreateSaleAsync(client, Payload(new DateTime(2099, 1, 1, 9, 0, 0, DateTimeKind.Utc)));

        var response = await client.GetAsync("/api/sales");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Page);
        Assert.Equal(20, body.PageSize);
        Assert.True(body.TotalCount >= 2);

        var newerIndex = IndexOf(body.Items, newer.Id);
        var olderIndex = IndexOf(body.Items, older.Id);
        Assert.True(newerIndex >= 0);
        Assert.True(olderIndex >= 0);
        Assert.True(newerIndex < olderIndex);
    }

    [Fact]
    public async Task ListSales_ComFiltrosDeClienteFilialECancelamento_DeveRetornarSomenteOSubconjuntoEsperado()
    {
        using var client = _factory.CreateClient();
        var customerId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var matching = await CreateSaleAsync(client, Payload(customerId: customerId, branchId: branchId));
        var otherCustomer = await CreateSaleAsync(client, Payload(branchId: branchId));
        var cancelledMatch = await CreateSaleAsync(client, Payload(customerId: customerId, branchId: branchId));
        await CancelSaleAsync(cancelledMatch.Id);

        var combinedResponse = await client.GetAsync($"/api/sales?customerId={customerId}&branchId={branchId}&isCancelled=false");
        Assert.Equal(HttpStatusCode.OK, combinedResponse.StatusCode);
        var combinedBody = await combinedResponse.Content.ReadFromJsonAsync<PagedResponse>();
        Assert.NotNull(combinedBody);
        Assert.True(IndexOf(combinedBody!.Items, matching.Id) >= 0);
        Assert.True(IndexOf(combinedBody.Items, otherCustomer.Id) < 0);
        Assert.True(IndexOf(combinedBody.Items, cancelledMatch.Id) < 0);

        var cancelledResponse = await client.GetAsync($"/api/sales?customerId={customerId}&branchId={branchId}&isCancelled=true");
        Assert.Equal(HttpStatusCode.OK, cancelledResponse.StatusCode);
        var cancelledBody = await cancelledResponse.Content.ReadFromJsonAsync<PagedResponse>();
        Assert.NotNull(cancelledBody);
        Assert.True(IndexOf(cancelledBody!.Items, cancelledMatch.Id) >= 0);
        Assert.True(IndexOf(cancelledBody.Items, matching.Id) < 0);
    }

    [Fact]
    public async Task ListSales_ComParametrosInvalidosCombinados_DeveRetornar400ComUmaNotificationPorParametro()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/sales?pageSize=101&customerId=nao-e-um-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "pageSize");
        Assert.Contains(body.Errors, e => e.Key == "customerId");
    }

    private sealed record PagedResponse(SaleSummaryResponse[] Items, int Page, int PageSize, int TotalCount, int TotalPages);

    private sealed record ErrorResponse(ErrorItem[] Errors);

    private sealed record ErrorItem(string Key, string Message);
}
