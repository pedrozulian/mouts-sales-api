using System.Net;
using System.Net.Http.Json;
using SalesApi.Api.Tests.Infrastructure;
using SalesApi.Application.Sales.Dtos;

namespace SalesApi.Api.Tests.Sales;

public class CancelSaleItemEndpointTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public CancelSaleItemEndpointTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private static object ValidCreatePayload(int itemCount = 2)
    {
        var items = itemCount == 1
            ? new[]
            {
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" },
                    quantity = 2,
                    unitPrice = 250.00m,
                },
            }
            : new[]
            {
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" },
                    quantity = 1,
                    unitPrice = 250.00m,
                },
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Mousepad XL" },
                    quantity = 2,
                    unitPrice = 49.90m,
                },
            };

        return new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items,
        };
    }

    private static async Task<SaleResponse> CreateSaleAsync(HttpClient client, int itemCount = 2)
    {
        var response = await client.PostAsJsonAsync("/api/sales", ValidCreatePayload(itemCount));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(created);

        return created!;
    }

    [Fact]
    public async Task DeleteSaleItem_ComItemAtivoEntreOutros_DeveRetornar204ERecalcularTotal()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var targetItem = created.Items.First();
        var otherItem = created.Items.Single(i => i.Id != targetItem.Id);

        var response = await client.DeleteAsync($"/api/sales/{created.Id}/items/{targetItem.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);

        var getResponse = await client.GetAsync($"/api/sales/{created.Id}");
        var body = await getResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.False(body!.IsCancelled);
        Assert.True(body.Items.Single(i => i.Id == targetItem.Id).IsCancelled);
        Assert.False(body.Items.Single(i => i.Id == otherItem.Id).IsCancelled);
        Assert.Equal(otherItem.TotalAmount, body.TotalAmount);
    }

    [Fact]
    public async Task DeleteSaleItem_NoUltimoItemAtivo_DeveCancelarAVendaInteiraNaMesmaOperacao()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client, itemCount: 1);
        var item = created.Items.Single();

        var response = await client.DeleteAsync($"/api/sales/{created.Id}/items/{item.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync($"/api/sales/{created.Id}");
        var body = await getResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsCancelled);
        Assert.Equal(0.00m, body.TotalAmount);
        Assert.All(body.Items, i => Assert.True(i.IsCancelled));
    }

    [Fact]
    public async Task DeleteSaleItem_ComVendaInexistente_DeveRetornar404ComChaveId()
    {
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/sales/{Guid.NewGuid()}/items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "id");
    }

    [Fact]
    public async Task DeleteSaleItem_ComItemInexistenteNaVenda_DeveRetornar404ComChaveItemId()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);

        var response = await client.DeleteAsync($"/api/sales/{created.Id}/items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "itemId");
    }

    [Fact]
    public async Task DeleteSaleItem_ComVendaJaCancelada_DeveRetornar400ComChaveSale()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var item = created.Items.First();
        var cancelSale = await client.DeleteAsync($"/api/sales/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancelSale.StatusCode);

        var response = await client.DeleteAsync($"/api/sales/{created.Id}/items/{item.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "sale");
    }

    [Fact]
    public async Task DeleteSaleItem_ComItemJaCancelado_DeveRetornar400ComChaveItem()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var item = created.Items.First();
        var first = await client.DeleteAsync($"/api/sales/{created.Id}/items/{item.Id}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var response = await client.DeleteAsync($"/api/sales/{created.Id}/items/{item.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "item");
    }

    private sealed record ErrorResponse(ErrorItem[] Errors);

    private sealed record ErrorItem(string Key, string Message);
}
