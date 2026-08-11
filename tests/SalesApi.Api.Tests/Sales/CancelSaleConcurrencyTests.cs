using System.Net;
using System.Net.Http.Json;
using SalesApi.Api.Tests.Infrastructure;
using SalesApi.Application.Sales.Dtos;

namespace SalesApi.Api.Tests.Sales;

public class CancelSaleConcurrencyTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public CancelSaleConcurrencyTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private static object ValidPayload() => new
    {
        customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
        branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
        items = new[]
        {
            new
            {
                product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" },
                quantity = 2,
                unitPrice = 250.00m,
            },
        },
    };

    [Fact]
    public async Task DeleteSales_ComDuasRequisicoesConcorrentesParaAMesmaVenda_DeveAplicarApenasUmCancelamento()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/sales", ValidPayload());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(created);

        var tasks = Enumerable.Range(0, 2)
            .Select(_ => client.DeleteAsync($"/api/sales/{created!.Id}"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

        var getResponse = await client.GetAsync($"/api/sales/{created!.Id}");
        var body = await getResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsCancelled);
        Assert.Equal(0.00m, body.TotalAmount);
    }
}
