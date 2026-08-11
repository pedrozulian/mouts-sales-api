using System.Net;
using System.Net.Http.Json;
using SalesApi.Api.Tests.Infrastructure;
using SalesApi.Application.Sales.Dtos;

namespace SalesApi.Api.Tests.Sales;

public class CancelSaleEndpointTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public CancelSaleEndpointTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private static object ValidCreatePayload()
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

    private static async Task<SaleResponse> CreateSaleAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/sales", ValidCreatePayload());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(created);

        return created!;
    }

    [Fact]
    public async Task DeleteSales_ComVendaAtiva_DeveRetornar204ETotalAmountZeradoNaConsulta()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);

        var response = await client.DeleteAsync($"/api/sales/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);

        var getResponse = await client.GetAsync($"/api/sales/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsCancelled);
        Assert.Equal(0.00m, body.TotalAmount);
        Assert.All(body.Items, item => Assert.True(item.IsCancelled));
    }

    [Fact]
    public async Task DeleteSales_ComVendaInexistente_DeveRetornar404ComChaveId()
    {
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/sales/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "id");
    }

    [Fact]
    public async Task DeleteSales_ComVendaJaCancelada_DeveRetornar400ComChaveSale()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var first = await client.DeleteAsync($"/api/sales/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var response = await client.DeleteAsync($"/api/sales/{created.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "sale");
    }

    private sealed record ErrorResponse(ErrorItem[] Errors);

    private sealed record ErrorItem(string Key, string Message);
}
