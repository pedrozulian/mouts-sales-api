using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Api.Tests.Infrastructure;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Api.Tests.Sales;

public class GetSaleEndpointTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public GetSaleEndpointTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private static object ValidPayload()
    {
        return new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
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

    private static object TwoItemsPayload()
    {
        return new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" },
                    quantity = 10,
                    unitPrice = 250.00m,
                },
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Mousepad XL" },
                    quantity = 2,
                    unitPrice = 49.90m,
                },
            },
        };
    }

    private static async Task<SaleResponse> CreateSaleAsync(HttpClient client, object? payload = null)
    {
        var createResponse = await client.PostAsJsonAsync("/api/sales", payload ?? ValidPayload());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(created);

        return created!;
    }

    private async Task CancelItemAsync(Guid saleId, Guid itemId, decimal newSaleTotal)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sale = await context.Sales.Include(s => s.Items).SingleAsync(s => s.Id == saleId);
        var item = sale.Items.Single(i => i.Id == itemId);

        context.Entry(item).Property(nameof(SaleItem.IsCancelled)).CurrentValue = true;
        context.Entry(sale).Property(nameof(Sale.TotalAmount)).CurrentValue = newSaleTotal;

        await context.SaveChangesAsync();
    }

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
    public async Task GetSale_ComVendaExistente_DeveRetornar200ComCorpoIdenticoAoRegistrado()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);

        var response = await client.GetAsync($"/api/sales/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.Id);
        Assert.Equal(created.SaleNumber, body.SaleNumber);
        Assert.Equal(created.TotalAmount, body.TotalAmount);
        Assert.False(body.IsCancelled);

        var createdItem = Assert.Single(created.Items);
        var item = Assert.Single(body.Items);
        Assert.Equal(createdItem.Product.Id, item.Product.Id);
        Assert.Equal(createdItem.Product.Name, item.Product.Name);
        Assert.Equal(createdItem.DiscountPercentage, item.DiscountPercentage);
        Assert.Equal(createdItem.DiscountAmount, item.DiscountAmount);
        Assert.Equal(createdItem.TotalAmount, item.TotalAmount);
    }

    [Fact]
    public async Task GetSale_ComItemCancelado_DeveRetornar200ComItemCanceladoVisivelETotalAtualizado()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client, TwoItemsPayload());
        var cancelledItem = created.Items.First();
        var remainingTotal = created.TotalAmount - cancelledItem.TotalAmount;

        await CancelItemAsync(created.Id, cancelledItem.Id, remainingTotal);

        var response = await client.GetAsync($"/api/sales/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.False(body!.IsCancelled);
        Assert.Equal(remainingTotal, body.TotalAmount);
        Assert.Equal(2, body.Items.Count);
        Assert.True(body.Items.Single(i => i.Id == cancelledItem.Id).IsCancelled);
        Assert.Contains(body.Items, i => i.Id != cancelledItem.Id && !i.IsCancelled);
    }

    [Fact]
    public async Task GetSale_ComVendaCanceladaIntegralmente_DeveRetornar200ComIsCancelledVerdadeiroETotalZerado()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);

        await CancelSaleAsync(created.Id);

        var response = await client.GetAsync($"/api/sales/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsCancelled);
        Assert.Equal(0m, body.TotalAmount);
        Assert.All(body.Items, i => Assert.True(i.IsCancelled));
    }

    [Fact]
    public async Task GetSale_ComIdInexistente_DeveRetornar404ComCorpoDeErroPadrao()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/sales/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, error => error.Key == "id");
    }

    [Fact]
    public async Task GetSale_ComIdEmFormatoInvalido_DeveRetornar404PeloRouteConstraint()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/sales/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ErrorResponse(ErrorItem[] Errors);

    private sealed record ErrorItem(string Key, string Message);
}
