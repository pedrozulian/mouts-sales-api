using SalesApi.Domain.Common;

namespace SalesApi.Domain.Sales;

public sealed class SaleItem : Entity
{
    public ExternalReference Product { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal DiscountPercentage { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public bool IsCancelled { get; private set; }

    private SaleItem()
    {
    }

    private SaleItem(Guid id, ExternalReference product, int quantity, decimal unitPrice) : base(id)
    {
        Product = product;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountPercentage = DiscountPolicy.GetPercentage(quantity);

        var grossAmount = unitPrice * quantity;
        DiscountAmount = grossAmount * DiscountPercentage;
        TotalAmount = grossAmount - DiscountAmount;
        IsCancelled = false;
    }

    public static Result<SaleItem> Create(ExternalReference product, int quantity, decimal unitPrice)
    {
        var errors = new List<Notification>();

        if (product is null || product.Id == Guid.Empty || string.IsNullOrWhiteSpace(product.Name))
        {
            errors.Add(new Notification("product", "Produto inválido: id e nome são obrigatórios."));
        }

        if (quantity > 20)
        {
            errors.Add(new Notification("quantity", "Não é possível vender mais de 20 unidades do mesmo produto."));
        }
        else if (quantity < 1)
        {
            errors.Add(new Notification("quantity", "A quantidade deve ser de ao menos 1 unidade."));
        }

        if (unitPrice <= 0)
        {
            errors.Add(new Notification("unitPrice", "O preço unitário deve ser maior que zero."));
        }

        if (errors.Count > 0)
        {
            return Result<SaleItem>.Failure(errors);
        }

        var item = new SaleItem(Guid.NewGuid(), product!, quantity, unitPrice);

        return Result<SaleItem>.Success(item);
    }
}
