using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Api.Tests.Infrastructure;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Api.Tests.Sales;

public class UpdateSaleEndpointTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public UpdateSaleEndpointTests(SalesApiFactory factory)
    {
        _factory = factory;
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
                    quantity = 2,
                    unitPrice = 250.00m,
                },
            },
        };
    }

    private static async Task<SaleResponse> CreateSaleAsync(HttpClient client, object? payload = null)
    {
        var response = await client.PostAsJsonAsync("/api/sales", payload ?? ValidCreatePayload());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(created);

        return created!;
    }

    [Fact]
    public async Task PutSales_ComReconciliacaoCompleta_DeveRetornar200ComVendaAtualizada()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var existingItem = created.Items.Single();
        var newProductId = Guid.NewGuid();

        var payload = new
        {
            saleDate = created.SaleDate,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = new object[]
            {
                new
                {
                    id = existingItem.Id,
                    product = new { id = existingItem.Product.Id, name = existingItem.Product.Name },
                    quantity = 12,
                    unitPrice = 250.00m,
                },
                new
                {
                    product = new { id = newProductId, name = "Headset Gamer H5" },
                    quantity = 3,
                    unitPrice = 180.00m,
                },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SaleResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Items.Count);
        Assert.Equal(2400.00m + 540.00m, body.TotalAmount);

        var updatedItem = body.Items.Single(i => i.Id == existingItem.Id);
        Assert.Equal(12, updatedItem.Quantity);
        Assert.Equal(2400.00m, updatedItem.TotalAmount);
        Assert.False(updatedItem.IsCancelled);

        var newItem = body.Items.Single(i => i.Product.Id == newProductId);
        Assert.Equal(3, newItem.Quantity);
        Assert.Equal(540.00m, newItem.TotalAmount);
        Assert.False(newItem.IsCancelled);
    }

    [Fact]
    public async Task PutSales_ComVendaInexistente_DeveRetornar404ComChaveId()
    {
        using var client = _factory.CreateClient();

        var payload = new
        {
            saleDate = DateTime.UtcNow,
            customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
            branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
            items = new[]
            {
                new { product = new { id = Guid.NewGuid(), name = "Produto" }, quantity = 1, unitPrice = 10.00m },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{Guid.NewGuid()}", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "id");
    }

    [Fact]
    public async Task PutSales_ComVendaJaCancelada_DeveRetornar400ComChaveSale()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        await CancelSaleAsync(created.Id);

        var payload = new
        {
            saleDate = created.SaleDate,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = new[]
            {
                new { product = new { id = Guid.NewGuid(), name = "Produto" }, quantity = 1, unitPrice = 10.00m },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "sale");
    }

    [Fact]
    public async Task PutSales_SemItens_DeveRetornar400ComChaveItems()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);

        var payload = new
        {
            saleDate = created.SaleDate,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = Array.Empty<object>(),
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items");
    }

    [Fact]
    public async Task PutSales_ComItemIdInexistenteNaVenda_DeveRetornar400ComChaveItemsItemId()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);

        var payload = new
        {
            saleDate = created.SaleDate,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    product = new { id = created.Items.Single().Product.Id, name = created.Items.Single().Product.Name },
                    quantity = 2,
                    unitPrice = 250.00m,
                },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items[0].id");
    }

    [Fact]
    public async Task PutSales_AlterandoProdutoDeItemExistente_DeveRetornar400ComChaveItemsProductId()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var existingItem = created.Items.Single();

        var payload = new
        {
            saleDate = created.SaleDate,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = new[]
            {
                new
                {
                    id = existingItem.Id,
                    product = new { id = Guid.NewGuid(), name = "Outro Produto" },
                    quantity = 2,
                    unitPrice = 250.00m,
                },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items[0].product.id");
    }

    [Fact]
    public async Task PutSales_ComQuantidadeAcimaDoLimite_DeveRetornar400ComChaveItemsQuantity()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var existingItem = created.Items.Single();

        var payload = new
        {
            saleDate = created.SaleDate,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = new[]
            {
                new
                {
                    id = existingItem.Id,
                    product = new { id = existingItem.Product.Id, name = existingItem.Product.Name },
                    quantity = 21,
                    unitPrice = 250.00m,
                },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items[0].quantity");
    }

    [Fact]
    public async Task PutSales_ComProdutoDuplicadoNoCorpo_DeveRetornar400ComChaveItemsProductId()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var existingItem = created.Items.Single();

        var payload = new
        {
            saleDate = created.SaleDate,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = new[]
            {
                new
                {
                    id = (Guid?)existingItem.Id,
                    product = new { id = existingItem.Product.Id, name = existingItem.Product.Name },
                    quantity = 2,
                    unitPrice = 250.00m,
                },
                new
                {
                    id = (Guid?)null,
                    product = new { id = existingItem.Product.Id, name = existingItem.Product.Name },
                    quantity = 1,
                    unitPrice = 250.00m,
                },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "items[1].product.id");
    }

    [Fact]
    public async Task PutSales_SemSaleDate_DeveRetornar400ComChaveSaleDate()
    {
        using var client = _factory.CreateClient();
        var created = await CreateSaleAsync(client);
        var existingItem = created.Items.Single();

        var payload = new
        {
            saleDate = (DateTime?)null,
            customer = new { id = created.Customer.Id, name = created.Customer.Name },
            branch = new { id = created.Branch.Id, name = created.Branch.Name },
            items = new[]
            {
                new
                {
                    id = existingItem.Id,
                    product = new { id = existingItem.Product.Id, name = existingItem.Product.Name },
                    quantity = 2,
                    unitPrice = 250.00m,
                },
            },
        };

        var response = await client.PutAsJsonAsync($"/api/sales/{created.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains(body!.Errors, e => e.Key == "saleDate");
    }

    private sealed record ErrorResponse(ErrorItem[] Errors);

    private sealed record ErrorItem(string Key, string Message);
}
