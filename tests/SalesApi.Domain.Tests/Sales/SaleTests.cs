using SalesApi.Domain.Sales;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Domain.Tests.Sales;

public class SaleTests
{
    private static ExternalReference Customer() => new(Guid.NewGuid(), "Maria Souza");

    private static ExternalReference Branch() => new(Guid.NewGuid(), "Filial Centro");

    private static ExternalReference Product(string name = "Produto") => new(Guid.NewGuid(), name);

    [Fact]
    public void Create_ComUmItem_DeveCalcularTotalDaVendaComoTotalDoItem()
    {
        var items = new[] { new SaleItemInput(Product(), 2, 250.00m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000001");

        Assert.True(result.IsSuccess);
        var sale = result.Value!;
        Assert.Single(sale.Items);
        Assert.Equal(500.00m, sale.TotalAmount);
    }

    [Fact]
    public void Create_ComMultiplosItensDeProdutosDiferentes_TotalDaVendaDeveSerASomaDosTotaisDosItens()
    {
        var items = new[]
        {
            new SaleItemInput(Product("Teclado Mecânico K68"), 10, 250.00m),
            new SaleItemInput(Product("Mousepad XL"), 2, 49.90m),
        };

        var result = Sale.Create(Customer(), Branch(), items, "V-000002");

        Assert.True(result.IsSuccess);
        var sale = result.Value!;
        Assert.Equal(2, sale.Items.Count);
        Assert.Equal(2099.80m, sale.TotalAmount);
    }

    [Fact]
    public void Create_SemSaleDateInformada_DeveAssumirOMomentoDoRegistro()
    {
        var before = DateTime.UtcNow;
        var items = new[] { new SaleItemInput(Product(), 1, 10.00m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000003");

        var after = DateTime.UtcNow;
        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value!.SaleDate, before, after);
    }

    [Fact]
    public void Create_ComSaleDateInformada_DeveUsarODataInformada()
    {
        var saleDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var items = new[] { new SaleItemInput(Product(), 1, 10.00m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000004", saleDate);

        Assert.True(result.IsSuccess);
        Assert.Equal(saleDate, result.Value!.SaleDate);
    }

    [Fact]
    public void Create_SemItens_DeveFalharComChaveItems()
    {
        var result = Sale.Create(Customer(), Branch(), Array.Empty<SaleItemInput>(), "V-000005");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items");
    }

    [Fact]
    public void Create_ComQuantidadeAcimaDoLimite_DeveFalharComChaveItemsQuantity()
    {
        var items = new[] { new SaleItemInput(Product(), 21, 10.00m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000006");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[0].quantity");
    }

    [Fact]
    public void Create_ComProdutoDuplicadoEntreItens_DeveFalharComChaveItemsProductId()
    {
        var product = Product();
        var items = new[]
        {
            new SaleItemInput(product, 1, 10.00m),
            new SaleItemInput(product, 2, 20.00m),
        };

        var result = Sale.Create(Customer(), Branch(), items, "V-000007");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[1].product.id");
    }

    [Fact]
    public void Create_ComPrecoUnitarioMenorOuIgualAZero_DeveFalharComChaveItemsUnitPrice()
    {
        var items = new[] { new SaleItemInput(Product(), 1, 0m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000008");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[0].unitPrice");
    }

    [Fact]
    public void Create_ComClienteComIdVazio_DeveFalharComChaveCustomer()
    {
        var items = new[] { new SaleItemInput(Product(), 1, 10.00m) };

        var result = Sale.Create(new ExternalReference(Guid.Empty, "Maria Souza"), Branch(), items, "V-000009");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "customer");
    }

    [Fact]
    public void Create_ComFilialComNomeVazio_DeveFalharComChaveBranch()
    {
        var items = new[] { new SaleItemInput(Product(), 1, 10.00m) };

        var result = Sale.Create(Customer(), new ExternalReference(Guid.NewGuid(), string.Empty), items, "V-000010");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "branch");
    }

    [Fact]
    public void Create_ComProdutoComIdVazio_DeveFalharComChaveItemsProduct()
    {
        var items = new[] { new SaleItemInput(new ExternalReference(Guid.Empty, "Produto"), 1, 10.00m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000011");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[0].product");
    }

    [Fact]
    public void Create_ComRequisicaoInvalida_NaoDeveRetornarItensParciais()
    {
        var items = new[] { new SaleItemInput(Product(), 21, 10.00m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000012");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Create_ComSucesso_DeveRegistrarEventoSaleCreated()
    {
        var items = new[] { new SaleItemInput(Product(), 1, 10.00m) };

        var result = Sale.Create(Customer(), Branch(), items, "V-000013");

        Assert.True(result.IsSuccess);
        var sale = result.Value!;
        var domainEvent = Assert.Single(sale.DomainEvents);
        var saleCreated = Assert.IsType<SaleCreated>(domainEvent);
        Assert.Equal(sale.Id, saleCreated.SaleId);
        Assert.Equal(sale.SaleNumber, saleCreated.SaleNumber);
        Assert.Equal(sale.Customer.Id, saleCreated.CustomerId);
        Assert.Equal(sale.Branch.Id, saleCreated.BranchId);
        Assert.Equal(sale.TotalAmount, saleCreated.TotalAmount);
    }

    [Fact]
    public void Create_QuandoFalha_NaoDeveRegistrarNenhumEvento()
    {
        var result = Sale.Create(Customer(), Branch(), Array.Empty<SaleItemInput>(), "V-000014");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
