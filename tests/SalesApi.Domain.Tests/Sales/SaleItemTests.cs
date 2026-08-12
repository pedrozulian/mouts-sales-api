using SalesApi.Domain.Sales;

namespace SalesApi.Domain.Tests.Sales;

public class SaleItemTests
{
    private static ExternalReference Product(string name = "Produto") => new(Guid.NewGuid(), name);

    [Fact]
    public void Create_ComDescontoNoPontoMedioExato_DeveArredondarParaCimaEmValorAbsoluto()
    {
        var result = SaleItem.Create(Product(), 5, 12.35m);

        Assert.True(result.IsSuccess);
        var item = result.Value!;
        Assert.Equal(6.18m, item.DiscountAmount);
        Assert.Equal(55.57m, item.TotalAmount);
    }

    [Fact]
    public void Create_ComDescontoSemFracaoDeCentavo_NaoDeveAlterarOValor()
    {
        var result = SaleItem.Create(Product(), 10, 250.00m);

        Assert.True(result.IsSuccess);
        var item = result.Value!;
        Assert.Equal(500.00m, item.DiscountAmount);
        Assert.Equal(2000.00m, item.TotalAmount);
    }

    [Fact]
    public void ApplyChange_ComDescontoNoPontoMedioExato_DeveArredondarParaCimaEmValorAbsoluto()
    {
        var result = SaleItem.Create(Product(), 1, 10.00m);
        var item = result.Value!;

        item.ApplyChange(5, 12.35m);

        Assert.Equal(6.18m, item.DiscountAmount);
        Assert.Equal(55.57m, item.TotalAmount);
    }
}
