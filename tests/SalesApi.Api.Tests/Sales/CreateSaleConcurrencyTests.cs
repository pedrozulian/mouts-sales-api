using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SalesApi.Application.Sales.Dtos;

namespace SalesApi.Api.Tests.Sales;

public class CreateSaleConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CreateSaleConcurrencyTests(WebApplicationFactory<Program> factory)
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
    public async Task PostSales_ComRequisicoesConcorrentes_DeveGerarSaleNumbersDistintos()
    {
        using var client = _factory.CreateClient();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => client.PostAsJsonAsync("/api/sales", ValidPayload()))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var saleNumbers = await Task.WhenAll(responses.Select(async r =>
        {
            var body = await r.Content.ReadFromJsonAsync<SaleResponse>();
            return body!.SaleNumber;
        }));

        Assert.Equal(saleNumbers.Length, saleNumbers.Distinct().Count());
    }
}
