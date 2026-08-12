using System.Reflection;
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
    public void Create_ComItensCujosTotaisForamArredondados_TotalDaVendaDeveSerASomaExataDosItens()
    {
        var items = new[]
        {
            new SaleItemInput(Product("Item A"), 4, 12.34m),
            new SaleItemInput(Product("Item B"), 4, 12.34m),
        };

        var result = Sale.Create(Customer(), Branch(), items, "V-000099");

        Assert.True(result.IsSuccess);
        var sale = result.Value!;
        Assert.All(sale.Items, item => Assert.Equal(44.42m, item.TotalAmount));
        Assert.Equal(88.84m, sale.TotalAmount);
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

    // --- Update (005-alterar-venda) ---

    private static Sale CreateSale(params SaleItemInput[] items)
    {
        var result = Sale.Create(Customer(), Branch(), items, "V-100000", DateTime.UtcNow);
        Assert.True(result.IsSuccess);

        return result.Value!;
    }

    // Cancelamento de venda (UC-05) ainda não existe no agregado — mesmo workaround de
    // 003-consultar-venda/research.md (seção 6), adaptado via reflection por não haver um
    // DbContext disponível neste teste de Domain puro.
    private static void ForceCancelled(Sale sale)
    {
        typeof(Sale).GetProperty(nameof(Sale.IsCancelled))!.GetSetMethod(nonPublic: true)!.Invoke(sale, new object[] { true });
    }

    [Fact]
    public void Update_ComItemExistenteReferenciadoPorItemId_DeveAtualizarQuantidadeERecalcularDescontoETotal()
    {
        var product = Product("Teclado Mecânico K68");
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, product, 12, 250.00m) });

        Assert.True(result.IsSuccess);
        var item = sale.Items.Single();
        Assert.Equal(12, item.Quantity);
        Assert.Equal(0.20m, item.DiscountPercentage);
        Assert.Equal(2400.00m, item.TotalAmount);
        Assert.Equal(2400.00m, sale.TotalAmount);
    }

    [Fact]
    public void Update_ComQuantidadeQueMudaDeFaixaDeDescontoEProduzPontoMedioExato_DeveArredondarParaCimaEmValorAbsoluto()
    {
        var product = Product("Teclado Mecânico K68");
        var sale = CreateSale(new SaleItemInput(product, 1, 10.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, product, 5, 12.35m) });

        Assert.True(result.IsSuccess);
        var item = sale.Items.Single();
        Assert.Equal(6.18m, item.DiscountAmount);
        Assert.Equal(55.57m, item.TotalAmount);
        Assert.Equal(55.57m, sale.TotalAmount);
    }

    [Fact]
    public void Update_ComItemSemItemId_DeveAdicionarComoNovoItem()
    {
        var existingProduct = Product("Teclado Mecânico K68");
        var sale = CreateSale(new SaleItemInput(existingProduct, 2, 250.00m));
        var existingItemId = sale.Items.Single().Id;
        var newProduct = Product("Headset Gamer H5");

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[]
            {
                new SaleItemChangeInput(existingItemId, existingProduct, 2, 250.00m),
                new SaleItemChangeInput(null, newProduct, 3, 180.00m),
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sale.Items.Count);
        var newItem = sale.Items.Single(i => i.Product.Id == newProduct.Id);
        Assert.Equal(3, newItem.Quantity);
        Assert.Equal(540.00m, newItem.TotalAmount);
        Assert.Equal(500.00m + 540.00m, sale.TotalAmount);
    }

    [Fact]
    public void Update_ComItemAtivoAusenteDoCorpo_DeveCancelarImplicitamenteERecalcularTotal()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var sale = CreateSale(
            new SaleItemInput(product1, 2, 250.00m),
            new SaleItemInput(product2, 2, 49.90m));
        var item1Id = sale.Items.Single(i => i.Product.Id == product1.Id).Id;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(item1Id, product1, 2, 250.00m) });

        Assert.True(result.IsSuccess);
        var cancelledItem = sale.Items.Single(i => i.Product.Id == product2.Id);
        Assert.True(cancelledItem.IsCancelled);
        Assert.Equal(500.00m, sale.TotalAmount);
    }

    [Fact]
    public void Update_ComNovoCabecalho_DeveAtualizarClienteFilialDataSemAfetarItensNaoAlterados()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;
        var novoCliente = new ExternalReference(Guid.NewGuid(), "João Pereira");
        var novaFilial = new ExternalReference(Guid.NewGuid(), "Filial Norte");
        var novaData = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        var result = sale.Update(
            novoCliente,
            novaFilial,
            novaData,
            new[] { new SaleItemChangeInput(itemId, product, 2, 250.00m) });

        Assert.True(result.IsSuccess);
        Assert.Equal(novoCliente, sale.Customer);
        Assert.Equal(novaFilial, sale.Branch);
        Assert.Equal(novaData, sale.SaleDate);
        Assert.Equal(500.00m, sale.Items.Single().TotalAmount);
    }

    [Fact]
    public void Update_ComVendaCancelada_DeveFalharComChaveSaleSemMutarNada()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;
        ForceCancelled(sale);
        var totalAntes = sale.TotalAmount;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, product, 5, 250.00m) });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "sale");
        Assert.Equal(2, sale.Items.Single().Quantity);
        Assert.Equal(totalAntes, sale.TotalAmount);
    }

    [Fact]
    public void Update_ComCorpoDeItensVazio_DeveFalharComChaveItems()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));

        var result = sale.Update(sale.Customer, sale.Branch, sale.SaleDate, Array.Empty<SaleItemChangeInput>());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items");
    }

    [Fact]
    public void Update_ComItemIdQueNaoPertenceAVenda_DeveFalharComChaveItemsItemId()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(Guid.NewGuid(), product, 2, 250.00m) });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[0].id");
    }

    [Fact]
    public void Update_ComItemIdDeItemJaCancelado_DeveFalharComChaveItemsItemIdSemReativar()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var sale = CreateSale(
            new SaleItemInput(product1, 2, 250.00m),
            new SaleItemInput(product2, 2, 49.90m));
        var item1Id = sale.Items.Single(i => i.Product.Id == product1.Id).Id;
        var item2Id = sale.Items.Single(i => i.Product.Id == product2.Id).Id;

        var firstUpdate = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(item1Id, product1, 2, 250.00m) });
        Assert.True(firstUpdate.IsSuccess);
        Assert.True(sale.Items.Single(i => i.Id == item2Id).IsCancelled);

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[]
            {
                new SaleItemChangeInput(item1Id, product1, 2, 250.00m),
                new SaleItemChangeInput(item2Id, product2, 3, 49.90m),
            });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[1].id");
        Assert.True(sale.Items.Single(i => i.Id == item2Id).IsCancelled);
    }

    [Fact]
    public void Update_AlterandoProdutoDeItemExistente_DeveFalharComChaveItemsProductId()
    {
        var product = Product("Teclado Mecânico K68");
        var outroProduto = Product("Mousepad XL");
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, outroProduto, 2, 250.00m) });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[0].product.id");
        Assert.Equal(product.Id, sale.Items.Single().Product.Id);
    }

    [Fact]
    public void Update_ReintroduzindoProdutoDeItemCancelado_DeveFalharComChaveItemsProductId()
    {
        var product1 = Product("Item A");
        var product2 = Product("Item B");
        var sale = CreateSale(
            new SaleItemInput(product1, 2, 10.00m),
            new SaleItemInput(product2, 2, 10.00m));
        var item1Id = sale.Items.Single(i => i.Product.Id == product1.Id).Id;
        var item2Id = sale.Items.Single(i => i.Product.Id == product2.Id).Id;
        sale.CancelItem(item1Id);

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[]
            {
                new SaleItemChangeInput(item2Id, product2, 2, 10.00m),
                new SaleItemChangeInput(null, product1, 3, 10.00m),
            });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[1].product.id");
    }

    [Fact]
    public void Update_ComProdutoNovoQueNaoPertenceANenhumItemExistente_DeveAdicionarNormalmente()
    {
        var product1 = Product("Item A");
        var product2 = Product("Item B");
        var produtoNovo = Product("Item C");
        var sale = CreateSale(
            new SaleItemInput(product1, 2, 10.00m),
            new SaleItemInput(product2, 2, 10.00m));
        var item1Id = sale.Items.Single(i => i.Product.Id == product1.Id).Id;
        var item2Id = sale.Items.Single(i => i.Product.Id == product2.Id).Id;
        sale.CancelItem(item1Id);

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[]
            {
                new SaleItemChangeInput(item2Id, product2, 2, 10.00m),
                new SaleItemChangeInput(null, produtoNovo, 3, 10.00m),
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, sale.Items.Count);
        Assert.Contains(sale.Items, i => i.Product.Id == produtoNovo.Id && !i.IsCancelled);
    }

    [Fact]
    public void Update_ReintroduzindoProdutoDeItemCancelado_NaoDeveMutarNenhumEstadoDaVenda()
    {
        var product1 = Product("Item A");
        var product2 = Product("Item B");
        var sale = CreateSale(
            new SaleItemInput(product1, 2, 10.00m),
            new SaleItemInput(product2, 2, 10.00m));
        var item1Id = sale.Items.Single(i => i.Product.Id == product1.Id).Id;
        var item2Id = sale.Items.Single(i => i.Product.Id == product2.Id).Id;
        sale.CancelItem(item1Id);
        var totalAntes = sale.TotalAmount;
        var updatedAtAntes = sale.UpdatedAt;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[]
            {
                new SaleItemChangeInput(item2Id, product2, 2, 10.00m),
                new SaleItemChangeInput(null, product1, 3, 10.00m),
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(2, sale.Items.Count);
        Assert.True(sale.Items.Single(i => i.Id == item1Id).IsCancelled);
        Assert.Equal(2, sale.Items.Single(i => i.Id == item2Id).Quantity);
        Assert.Equal(totalAntes, sale.TotalAmount);
        Assert.Equal(updatedAtAntes, sale.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Update_ComQuantidadeForaDoIntervalo_DeveFalharComChaveItemsQuantity(int quantidadeInvalida)
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, product, quantidadeInvalida, 250.00m) });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[0].quantity");
    }

    [Fact]
    public void Update_ComProdutoDuplicadoNoCorpo_DeveFalharComChaveItemsProductId()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[]
            {
                new SaleItemChangeInput(itemId, product, 2, 250.00m),
                new SaleItemChangeInput(null, product, 1, 10.00m),
            });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "items[1].product.id");
    }

    [Fact]
    public void Update_SemSaleDate_DeveFalharComChaveSaleDate()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            null,
            new[] { new SaleItemChangeInput(itemId, product, 2, 250.00m) });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "saleDate");
    }

    [Fact]
    public void Update_ComClienteComIdVazio_DeveFalharComChaveCustomer()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            new ExternalReference(Guid.Empty, "Maria Souza"),
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, product, 2, 250.00m) });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "customer");
    }

    [Fact]
    public void Update_ComFilialComNomeVazio_DeveFalharComChaveBranch()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;

        var result = sale.Update(
            sale.Customer,
            new ExternalReference(Guid.NewGuid(), string.Empty),
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, product, 2, 250.00m) });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "branch");
    }

    [Fact]
    public void Update_RemovendoImplicitamenteDoisItens_DeveRegistrarDoisItemCancelledEUmSaleModified()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var product3 = Product("Fone P2");
        var sale = CreateSale(
            new SaleItemInput(product1, 2, 250.00m),
            new SaleItemInput(product2, 2, 49.90m),
            new SaleItemInput(product3, 1, 29.90m));
        var item1 = sale.Items.Single(i => i.Product.Id == product1.Id);
        var item2 = sale.Items.Single(i => i.Product.Id == product2.Id);
        var item3 = sale.Items.Single(i => i.Product.Id == product3.Id);
        sale.ClearDomainEvents();

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(item1.Id, product1, 2, 250.00m) });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, sale.DomainEvents.Count);
        var itemCancelledEvents = sale.DomainEvents.OfType<ItemCancelled>().ToList();
        Assert.Equal(2, itemCancelledEvents.Count);
        Assert.Contains(itemCancelledEvents, e => e.SaleItemId == item2.Id && e.ProductId == product2.Id && e.Quantity == item2.Quantity);
        Assert.Contains(itemCancelledEvents, e => e.SaleItemId == item3.Id && e.ProductId == product3.Id && e.Quantity == item3.Quantity);
        var saleModified = Assert.Single(sale.DomainEvents.OfType<SaleModified>());
        Assert.Equal(sale.Id, saleModified.SaleId);
        Assert.Equal(sale.TotalAmount, saleModified.TotalAmount);
    }

    [Fact]
    public void Update_SemRemocaoImplicita_DeveRegistrarApenasUmSaleModified()
    {
        var product = Product();
        var sale = CreateSale(new SaleItemInput(product, 2, 250.00m));
        var itemId = sale.Items.Single().Id;
        sale.ClearDomainEvents();

        var result = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(itemId, product, 5, 250.00m) });

        Assert.True(result.IsSuccess);
        var domainEvent = Assert.Single(sale.DomainEvents);
        Assert.IsType<SaleModified>(domainEvent);
    }

    [Fact]
    public void Update_QuandoRejeitado_NaoDeveRegistrarNenhumEvento()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));
        sale.ClearDomainEvents();

        var result = sale.Update(sale.Customer, sale.Branch, sale.SaleDate, Array.Empty<SaleItemChangeInput>());

        Assert.False(result.IsSuccess);
        Assert.Empty(sale.DomainEvents);
    }

    // --- Cancel (006-cancelar-venda) ---

    [Fact]
    public void Cancel_ComVendaAtivaEItensAtivos_DeveCancelarVendaEItensEZerarTotal()
    {
        var sale = CreateSale(
            new SaleItemInput(Product("Teclado Mecânico K68"), 10, 250.00m),
            new SaleItemInput(Product("Mousepad XL"), 2, 49.90m));

        var result = sale.Cancel();

        Assert.True(result.IsSuccess);
        Assert.True(sale.IsCancelled);
        Assert.All(sale.Items, item => Assert.True(item.IsCancelled));
        Assert.Equal(0m, sale.TotalAmount);
    }

    [Fact]
    public void Cancel_ComItemJaCanceladoIndividualmente_DeveManterInalteradoAoCancelarVenda()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var sale = CreateSale(
            new SaleItemInput(product1, 2, 250.00m),
            new SaleItemInput(product2, 2, 49.90m));
        var item1Id = sale.Items.Single(i => i.Product.Id == product1.Id).Id;

        var updateResult = sale.Update(
            sale.Customer,
            sale.Branch,
            sale.SaleDate,
            new[] { new SaleItemChangeInput(item1Id, product1, 2, 250.00m) });
        Assert.True(updateResult.IsSuccess);
        var item2 = sale.Items.Single(i => i.Product.Id == product2.Id);
        Assert.True(item2.IsCancelled);

        var result = sale.Cancel();

        Assert.True(result.IsSuccess);
        Assert.True(item2.IsCancelled);
        Assert.True(sale.Items.Single(i => i.Product.Id == product1.Id).IsCancelled);
        Assert.True(sale.IsCancelled);
        Assert.Equal(0m, sale.TotalAmount);
    }

    [Fact]
    public void Cancel_ComVendaJaCancelada_DeveFalharComChaveSaleSemMutarNada()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));
        ForceCancelled(sale);
        var totalAntes = sale.TotalAmount;

        var result = sale.Cancel();

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "sale");
        Assert.Equal(totalAntes, sale.TotalAmount);
    }

    [Fact]
    public void Cancel_ComTresItensAtivos_DeveRegistrarApenasUmSaleCancelled()
    {
        var sale = CreateSale(
            new SaleItemInput(Product("A"), 1, 10.00m),
            new SaleItemInput(Product("B"), 1, 10.00m),
            new SaleItemInput(Product("C"), 1, 10.00m));
        sale.ClearDomainEvents();

        var result = sale.Cancel();

        Assert.True(result.IsSuccess);
        var domainEvent = Assert.Single(sale.DomainEvents);
        var saleCancelled = Assert.IsType<SaleCancelled>(domainEvent);
        Assert.Equal(sale.Id, saleCancelled.SaleId);
        Assert.Equal(sale.SaleNumber, saleCancelled.SaleNumber);
    }

    [Fact]
    public void Cancel_QuandoRejeitado_NaoDeveRegistrarNenhumEvento()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));
        ForceCancelled(sale);
        sale.ClearDomainEvents();

        var result = sale.Cancel();

        Assert.False(result.IsSuccess);
        Assert.Empty(sale.DomainEvents);
    }

    // --- CancelItem (007-cancelar-item-da-venda) ---

    [Fact]
    public void CancelItem_ComItemAtivoEntreOutrosAtivos_DeveCancelarApenasEsseItemERecalcularTotal()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var sale = CreateSale(
            new SaleItemInput(product1, 1, 250.00m),
            new SaleItemInput(product2, 2, 49.90m));
        var item1 = sale.Items.Single(i => i.Product.Id == product1.Id);
        var item2 = sale.Items.Single(i => i.Product.Id == product2.Id);

        var result = sale.CancelItem(item1.Id);

        Assert.True(result.IsSuccess);
        Assert.True(item1.IsCancelled);
        Assert.False(item2.IsCancelled);
        Assert.False(sale.IsCancelled);
        Assert.Equal(item2.TotalAmount, sale.TotalAmount);
    }

    [Fact]
    public void CancelItem_ComItemJaCanceladoIndividualmenteEntreOutros_DeveManterInalteradoAoCancelarOutroItem()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var product3 = Product("Fone P2");
        var sale = CreateSale(
            new SaleItemInput(product1, 1, 250.00m),
            new SaleItemInput(product2, 2, 49.90m),
            new SaleItemInput(product3, 1, 29.90m));
        var item1 = sale.Items.Single(i => i.Product.Id == product1.Id);
        var item2 = sale.Items.Single(i => i.Product.Id == product2.Id);
        var item3 = sale.Items.Single(i => i.Product.Id == product3.Id);
        var firstCancel = sale.CancelItem(item1.Id);
        Assert.True(firstCancel.IsSuccess);

        var result = sale.CancelItem(item2.Id);

        Assert.True(result.IsSuccess);
        Assert.True(item1.IsCancelled);
        Assert.True(item2.IsCancelled);
        Assert.False(item3.IsCancelled);
        Assert.Equal(item3.TotalAmount, sale.TotalAmount);
    }

    [Fact]
    public void CancelItem_NoUnicoItemAtivo_DeveCancelarItemEVendaEZerarTotal()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));
        var item = sale.Items.Single();

        var result = sale.CancelItem(item.Id);

        Assert.True(result.IsSuccess);
        Assert.True(item.IsCancelled);
        Assert.True(sale.IsCancelled);
        Assert.Equal(0m, sale.TotalAmount);
    }

    [Fact]
    public void CancelItem_NoUltimoItemAtivoComOutrosJaCanceladosIndividualmente_DeveCancelarAVenda()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var sale = CreateSale(
            new SaleItemInput(product1, 1, 250.00m),
            new SaleItemInput(product2, 2, 49.90m));
        var item1 = sale.Items.Single(i => i.Product.Id == product1.Id);
        var item2 = sale.Items.Single(i => i.Product.Id == product2.Id);
        var firstCancel = sale.CancelItem(item1.Id);
        Assert.True(firstCancel.IsSuccess);
        Assert.False(sale.IsCancelled);

        var result = sale.CancelItem(item2.Id);

        Assert.True(result.IsSuccess);
        Assert.True(item2.IsCancelled);
        Assert.True(sale.IsCancelled);
        Assert.Equal(0m, sale.TotalAmount);
    }

    [Fact]
    public void CancelItem_ComVendaJaCancelada_DeveFalharComChaveSaleSemMutarNada()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));
        var item = sale.Items.Single();
        ForceCancelled(sale);
        var totalAntes = sale.TotalAmount;

        var result = sale.CancelItem(item.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "sale");
        Assert.False(item.IsCancelled);
        Assert.Equal(totalAntes, sale.TotalAmount);
    }

    [Fact]
    public void CancelItem_ComItemIdInexistenteNaVenda_DeveFalharComChaveItemId()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));

        var result = sale.CancelItem(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "itemId");
    }

    [Fact]
    public void CancelItem_ComItemJaCancelado_DeveFalharComChaveItemSemAlterarTotalNemRegistrarEvento()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var sale = CreateSale(
            new SaleItemInput(product1, 1, 250.00m),
            new SaleItemInput(product2, 2, 49.90m));
        var item1 = sale.Items.Single(i => i.Product.Id == product1.Id);
        var firstCancel = sale.CancelItem(item1.Id);
        Assert.True(firstCancel.IsSuccess);
        var totalAntes = sale.TotalAmount;
        sale.ClearDomainEvents();

        var result = sale.CancelItem(item1.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Key == "item");
        Assert.Equal(totalAntes, sale.TotalAmount);
        Assert.Empty(sale.DomainEvents);
    }

    [Fact]
    public void CancelItem_ComSucessoSemEsgotarItensAtivos_DeveRegistrarApenasUmItemCancelled()
    {
        var product1 = Product("Teclado Mecânico K68");
        var product2 = Product("Mousepad XL");
        var sale = CreateSale(
            new SaleItemInput(product1, 1, 250.00m),
            new SaleItemInput(product2, 2, 49.90m));
        var item1 = sale.Items.Single(i => i.Product.Id == product1.Id);
        sale.ClearDomainEvents();

        var result = sale.CancelItem(item1.Id);

        Assert.True(result.IsSuccess);
        var domainEvent = Assert.Single(sale.DomainEvents);
        var itemCancelled = Assert.IsType<ItemCancelled>(domainEvent);
        Assert.Equal(sale.Id, itemCancelled.SaleId);
        Assert.Equal(item1.Id, itemCancelled.SaleItemId);
        Assert.Equal(product1.Id, itemCancelled.ProductId);
        Assert.Equal(item1.Quantity, itemCancelled.Quantity);
    }

    [Fact]
    public void CancelItem_NoUltimoItemAtivo_DeveRegistrarItemCancelledESaleCancelled()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));
        var item = sale.Items.Single();
        sale.ClearDomainEvents();

        var result = sale.CancelItem(item.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, sale.DomainEvents.Count);
        var itemCancelled = Assert.Single(sale.DomainEvents.OfType<ItemCancelled>());
        Assert.Equal(item.Id, itemCancelled.SaleItemId);
        var saleCancelled = Assert.Single(sale.DomainEvents.OfType<SaleCancelled>());
        Assert.Equal(sale.Id, saleCancelled.SaleId);
    }

    [Fact]
    public void CancelItem_QuandoRejeitado_NaoDeveRegistrarNenhumEvento()
    {
        var sale = CreateSale(new SaleItemInput(Product(), 2, 250.00m));
        ForceCancelled(sale);
        sale.ClearDomainEvents();
        var itemId = sale.Items.Single().Id;

        var result = sale.CancelItem(itemId);

        Assert.False(result.IsSuccess);
        Assert.Empty(sale.DomainEvents);
    }
}
