using SalesApi.Domain.Sales;

namespace SalesApi.Domain.Tests.Sales;

public class DiscountPolicyTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 0.10)]
    [InlineData(9, 0.10)]
    [InlineData(10, 0.20)]
    [InlineData(20, 0.20)]
    public void GetPercentage_DeveRetornarOPercentualDaFaixaCorrespondente(int quantity, decimal expected)
    {
        var percentage = DiscountPolicy.GetPercentage(quantity);

        Assert.Equal(expected, percentage);
    }
}
