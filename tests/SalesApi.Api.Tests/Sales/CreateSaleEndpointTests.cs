using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Api.Tests.Infrastructure;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Api.Tests.Sales;

public class CreateSaleEndpointTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public CreateSaleEndpointTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    private static object ValidPayload(int quantity, out decimal unitPrice)
    {
        unitPrice = 250.00m;

        return new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" },
                    quantity,
                    unitPrice,
                },
            },
        };
    }

    [Theory]
    [InlineData(2, 0.0)]
    [InlineData(5, 0.10)]
    [InlineData(15, 0.20)]
    public async Task PostSales_ComQuantidadeNaFaixaDeDesconto_DeveRetornar201ComDescontoETotalCorretos(
        int quantity, decimal expectedDiscountPercentage)
    {
        using var client = _factory.CreateClient();
        var payload = ValidPayload(quantity, out var unitPrice);

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body!.Items);

        var expectedGross = unitPrice * quantity;
        var expectedDiscountAmount = expectedGross * expectedDiscountPercentage;
        var expectedItemTotal = expectedGross - expectedDiscountAmount;

        Assert.Equal(expectedDiscountPercentage, item.DiscountPercentage);
        Assert.Equal(expectedItemTotal, item.TotalAmount);
        Assert.Equal(expectedItemTotal, body.TotalAmount);
        Assert.False(body.IsCancelled);
        Assert.Equal($"/api/sales/{body.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task PostSales_ComDescontoETotalArbitrariosNoPayload_DeveIgnorarEDevolverOsValoresCalculados()
    {
        using var client = _factory.CreateClient();

        var payload = new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Mousepad XL" },
                    quantity = 2,
                    unitPrice = 49.90m,
                    discountPercentage = 0.99m,
                    totalAmount = 1.00m,
                },
            },
            discountPercentage = 0.99m,
            totalAmount = 1.00m,
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body!.Items);

        Assert.Equal(0m, item.DiscountPercentage);
        Assert.Equal(99.80m, item.TotalAmount);
        Assert.Equal(99.80m, body.TotalAmount);
    }

    [Fact]
    public async Task PostSales_ComDescontoComMaisDeDuasCasasDecimais_DeveDevolverValoresIdenticosNaConsultaSubsequente()
    {
        using var client = _factory.CreateClient();

        var payload = new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new { product = new { id = Guid.NewGuid(), name = "Item A" }, quantity = 4, unitPrice = 12.34m },
                new { product = new { id = Guid.NewGuid(), name = "Item B" }, quantity = 4, unitPrice = 12.34m },
            },
        };

        var postResponse = await client.PostAsJsonAsync("/api/sales", payload);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var postBody = await postResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(postBody);

        Assert.Equal(88.84m, postBody!.TotalAmount);
        Assert.All(postBody.Items, item =>
        {
            Assert.Equal(4.94m, item.DiscountAmount);
            Assert.Equal(44.42m, item.TotalAmount);
        });

        var getResponse = await client.GetAsync($"/api/sales/{postBody.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(getBody);

        Assert.Equal(postBody.TotalAmount, getBody!.TotalAmount);
        for (var i = 0; i < postBody.Items.Count; i++)
        {
            Assert.Equal(postBody.Items.ElementAt(i).DiscountAmount, getBody.Items.ElementAt(i).DiscountAmount);
            Assert.Equal(postBody.Items.ElementAt(i).TotalAmount, getBody.Items.ElementAt(i).TotalAmount);
        }
    }

    [Fact]
    public async Task PostSales_ComQuantidadeAcimaDoLimite_DeveRetornar400ComChaveDaRegraViolada()
    {
        using var client = _factory.CreateClient();
        var payload = ValidPayload(21, out _);

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items[0].quantity");
    }

    [Fact]
    public async Task PostSales_ComProdutoDuplicado_DeveRetornar400ComChaveDaRegraViolada()
    {
        using var client = _factory.CreateClient();
        var product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" };

        var payload = new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new { product, quantity = 1, unitPrice = 100.00m },
                new { product, quantity = 2, unitPrice = 100.00m },
            },
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items[1].product.id");
    }

    [Fact]
    public async Task PostSales_SemItens_DeveRetornar400ComChaveItems()
    {
        using var client = _factory.CreateClient();

        var payload = new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = Array.Empty<object>(),
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items");
    }

    [Fact]
    public async Task PostSales_ComPrecoUnitarioInvalido_DeveRetornar400ComChaveItemsUnitPrice()
    {
        using var client = _factory.CreateClient();

        var payload = new
        {
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new
                {
                    product = new { id = Guid.NewGuid(), name = "Teclado Mecânico K68" },
                    quantity = 1,
                    unitPrice = 0m,
                },
            },
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items[0].unitPrice");
    }

    private sealed record ErrorResponse(ErrorItem[] Errors);

    private sealed record ErrorItem(string Key, string Message);

    private sealed class CapturingSaleCreatedHandler : INotificationHandler<SaleCreated>
    {
        private readonly List<SaleCreated> _events;

        public CapturingSaleCreatedHandler(List<SaleCreated> events) => _events = events;

        public Task Handle(SaleCreated notification, CancellationToken cancellationToken)
        {
            _events.Add(notification);

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PostSales_ComSucesso_DeveDispararEventoSaleCreatedComSaleIdESaleNumber()
    {
        var capturedEvents = new List<SaleCreated>();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
                services.AddSingleton<INotificationHandler<SaleCreated>>(new CapturingSaleCreatedHandler(capturedEvents)));
        });
        using var client = factory.CreateClient();
        var payload = ValidPayload(2, out _);

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);

        Assert.Contains(capturedEvents, e => e.SaleId == body!.Id && e.SaleNumber == body.SaleNumber);
    }
}
