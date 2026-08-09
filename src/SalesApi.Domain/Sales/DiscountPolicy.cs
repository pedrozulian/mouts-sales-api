namespace SalesApi.Domain.Sales;

public static class DiscountPolicy
{
    public static decimal GetPercentage(int quantity)
    {
        if (quantity >= 10 && quantity <= 20)
        {
            return 0.20m;
        }

        if (quantity >= 4 && quantity <= 9)
        {
            return 0.10m;
        }

        if (quantity >= 1 && quantity <= 3)
        {
            return 0m;
        }

        throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantidade fora da faixa suportada pela política de desconto.");
    }
}
