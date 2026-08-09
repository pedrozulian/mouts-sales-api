using SalesApi.Domain.Common;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Domain.Sales;

public sealed class Sale : Entity
{
    private readonly List<SaleItem> _items = new();

    public string SaleNumber { get; private set; } = string.Empty;

    public DateTime SaleDate { get; private set; }

    public ExternalReference Customer { get; private set; } = null!;

    public ExternalReference Branch { get; private set; } = null!;

    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();

    public decimal TotalAmount { get; private set; }

    public bool IsCancelled { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private Sale()
    {
    }

    private Sale(Guid id) : base(id)
    {
    }

    public static Result<Sale> Create(
        ExternalReference customer,
        ExternalReference branch,
        IReadOnlyCollection<SaleItemInput> items,
        string saleNumber,
        DateTime? saleDate = null)
    {
        var errors = new List<Notification>();

        ValidateCustomer(customer, errors);
        ValidateBranch(branch, errors);

        var createdItems = ValidateAndCreateItems(items, errors);

        if (errors.Count > 0)
        {
            return Result<Sale>.Failure(errors);
        }

        var now = DateTime.UtcNow;
        var sale = new Sale(Guid.NewGuid())
        {
            SaleNumber = saleNumber,
            SaleDate = saleDate ?? now,
            Customer = customer,
            Branch = branch,
            IsCancelled = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        sale._items.AddRange(createdItems);
        sale.TotalAmount = sale._items.Sum(i => i.TotalAmount);

        sale.AddDomainEvent(new SaleCreated(sale.Id, sale.SaleNumber, sale.Customer.Id, sale.Branch.Id, sale.TotalAmount));

        return Result<Sale>.Success(sale);
    }

    private static void ValidateCustomer(ExternalReference customer, List<Notification> errors)
    {
        if (customer is null || customer.Id == Guid.Empty || string.IsNullOrWhiteSpace(customer.Name))
        {
            errors.Add(new Notification("customer", "Cliente inválido: id e nome são obrigatórios."));
        }
    }

    private static void ValidateBranch(ExternalReference branch, List<Notification> errors)
    {
        if (branch is null || branch.Id == Guid.Empty || string.IsNullOrWhiteSpace(branch.Name))
        {
            errors.Add(new Notification("branch", "Filial inválida: id e nome são obrigatórios."));
        }
    }

    private static List<SaleItem> ValidateAndCreateItems(IReadOnlyCollection<SaleItemInput> items, List<Notification> errors)
    {
        var createdItems = new List<SaleItem>();

        if (items is null || items.Count == 0)
        {
            errors.Add(new Notification("items", "A venda deve ter ao menos um item."));

            return createdItems;
        }

        var seenProducts = new HashSet<Guid>();
        var index = 0;

        foreach (var item in items)
        {
            var itemResult = SaleItem.Create(item.Product!, item.Quantity, item.UnitPrice);

            if (itemResult.IsSuccess)
            {
                createdItems.Add(itemResult.Value!);
            }
            else
            {
                errors.AddRange(itemResult.Errors.Select(e => new Notification($"items[{index}].{e.Key}", e.Message)));
            }

            if (item.Product is not null && item.Product.Id != Guid.Empty && !seenProducts.Add(item.Product.Id))
            {
                errors.Add(new Notification($"items[{index}].product.id", "Produto duplicado entre os itens da venda."));
            }

            index++;
        }

        return createdItems;
    }
}
