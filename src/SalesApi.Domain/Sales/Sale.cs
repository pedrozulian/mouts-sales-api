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

    public Result<Sale> Update(
        ExternalReference customer,
        ExternalReference branch,
        DateTime? saleDate,
        IReadOnlyCollection<SaleItemChangeInput> items)
    {
        if (IsCancelled)
        {
            return Result<Sale>.Failure(new Notification("sale", "Venda cancelada não pode ser alterada."));
        }

        var errors = new List<Notification>();

        ValidateCustomer(customer, errors);
        ValidateBranch(branch, errors);
        ValidateSaleDate(saleDate, errors);
        ValidateItemsPresence(items, errors);

        var reconciliation = ReconcileItems(items ?? Array.Empty<SaleItemChangeInput>(), errors);

        if (errors.Count > 0)
        {
            return Result<Sale>.Failure(errors);
        }

        ApplyReconciliation(customer, branch, saleDate!.Value, reconciliation);

        return Result<Sale>.Success(this);
    }

    public Result<Sale> Cancel()
    {
        if (IsCancelled)
        {
            return Result<Sale>.Failure(new Notification("sale", "Venda já está cancelada."));
        }

        foreach (var item in _items.Where(i => !i.IsCancelled))
        {
            item.Cancel();
        }

        IsCancelled = true;
        TotalAmount = 0m;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SaleCancelled(Id, SaleNumber));

        return Result<Sale>.Success(this);
    }

    public Result<Sale> CancelItem(Guid itemId)
    {
        if (IsCancelled)
        {
            return Result<Sale>.Failure(new Notification("sale", "Venda cancelada não pode ter itens cancelados."));
        }

        var item = _items.FirstOrDefault(i => i.Id == itemId);

        if (item is null)
        {
            return Result<Sale>.Failure(new Notification("itemId", "Item não encontrado nesta venda."));
        }

        if (item.IsCancelled)
        {
            return Result<Sale>.Failure(new Notification("item", "Item já está cancelado."));
        }

        item.Cancel();
        TotalAmount = _items.Where(i => !i.IsCancelled).Sum(i => i.TotalAmount);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ItemCancelled(Id, item.Id, item.Product.Id, item.Quantity));

        return _items.All(i => i.IsCancelled) ? Cancel() : Result<Sale>.Success(this);
    }

    private ItemReconciliation ReconcileItems(IReadOnlyCollection<SaleItemChangeInput> items, List<Notification> errors)
    {
        var reconciliation = new ItemReconciliation();
        var seenProducts = new HashSet<Guid>();
        var index = 0;

        foreach (var itemInput in items)
        {
            if (itemInput.Id is Guid id)
            {
                ReconcileExistingItem(id, itemInput, index, seenProducts, reconciliation, errors);
            }
            else
            {
                ReconcileNewItem(itemInput, index, seenProducts, reconciliation, errors);
            }

            index++;
        }

        return reconciliation;
    }

    private void ReconcileExistingItem(
        Guid id,
        SaleItemChangeInput itemInput,
        int index,
        HashSet<Guid> seenProducts,
        ItemReconciliation reconciliation,
        List<Notification> errors)
    {
        reconciliation.ReferencedIds.Add(id);
        var existingItem = _items.FirstOrDefault(i => i.Id == id);

        if (existingItem is null || existingItem.IsCancelled)
        {
            errors.Add(new Notification(
                $"items[{index}].id",
                existingItem is null ? "Item não pertence a esta venda." : "Item já está cancelado e não pode ser alterado."));

            return;
        }

        if (itemInput.Product is null || existingItem.Product.Id != itemInput.Product.Id)
        {
            errors.Add(new Notification($"items[{index}].product.id", "Produto de um item existente não pode ser alterado."));

            return;
        }

        if (itemInput.Quantity > 20)
        {
            errors.Add(new Notification($"items[{index}].quantity", "Não é possível vender mais de 20 unidades do mesmo produto."));
        }
        else if (itemInput.Quantity < 1)
        {
            errors.Add(new Notification($"items[{index}].quantity", "A quantidade deve ser de ao menos 1 unidade."));
        }
        else if (itemInput.UnitPrice <= 0)
        {
            errors.Add(new Notification($"items[{index}].unitPrice", "O preço unitário deve ser maior que zero."));
        }
        else
        {
            reconciliation.Updates.Add((existingItem, itemInput.Quantity, itemInput.UnitPrice));
        }

        if (!seenProducts.Add(existingItem.Product.Id))
        {
            errors.Add(new Notification($"items[{index}].product.id", "Produto duplicado entre os itens da venda."));
        }
    }

    private void ReconcileNewItem(
        SaleItemChangeInput itemInput,
        int index,
        HashSet<Guid> seenProducts,
        ItemReconciliation reconciliation,
        List<Notification> errors)
    {
        var itemResult = SaleItem.Create(itemInput.Product, itemInput.Quantity, itemInput.UnitPrice);

        if (itemResult.IsSuccess)
        {
            reconciliation.NewItems.Add(itemResult.Value!);
        }
        else
        {
            errors.AddRange(itemResult.Errors.Select(e => new Notification($"items[{index}].{e.Key}", e.Message)));
        }

        // O produto de um item novo não pode coincidir com o de nenhum item já pertencente à
        // venda — ativo ou cancelado (INV-03). Checar só contra os produtos vistos nesta
        // requisição deixaria reintroduzir o produto de um item cancelado, que só seria barrado
        // pelo índice único do banco — como exception de infraestrutura, não Notification.
        if (itemInput.Product is not null && itemInput.Product.Id != Guid.Empty)
        {
            var produtoJaPertenceAVenda = _items.Any(i => i.Product.Id == itemInput.Product.Id)
                || !seenProducts.Add(itemInput.Product.Id);

            if (produtoJaPertenceAVenda)
            {
                errors.Add(new Notification($"items[{index}].product.id", "Produto já pertence a esta venda."));
            }
        }
    }

    private void ApplyReconciliation(
        ExternalReference customer,
        ExternalReference branch,
        DateTime saleDate,
        ItemReconciliation reconciliation)
    {
        Customer = customer;
        Branch = branch;
        SaleDate = saleDate;

        var implicitlyCancelledItems = _items
            .Where(i => !i.IsCancelled && !reconciliation.ReferencedIds.Contains(i.Id))
            .ToList();

        foreach (var (item, quantity, unitPrice) in reconciliation.Updates)
        {
            item.ApplyChange(quantity, unitPrice);
        }

        _items.AddRange(reconciliation.NewItems);

        foreach (var item in implicitlyCancelledItems)
        {
            item.Cancel();
            AddDomainEvent(new ItemCancelled(Id, item.Id, item.Product.Id, item.Quantity));
        }

        TotalAmount = _items.Where(i => !i.IsCancelled).Sum(i => i.TotalAmount);
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SaleModified(Id, SaleNumber, TotalAmount));
    }

    private static void ValidateSaleDate(DateTime? saleDate, List<Notification> errors)
    {
        if (saleDate is null)
        {
            errors.Add(new Notification("saleDate", "Data da venda é obrigatória."));
        }
    }

    private static void ValidateItemsPresence(IReadOnlyCollection<SaleItemChangeInput>? items, List<Notification> errors)
    {
        if (items is null || items.Count == 0)
        {
            errors.Add(new Notification("items", "A venda deve ter ao menos um item."));
        }
    }

    private sealed class ItemReconciliation
    {
        public List<(SaleItem Item, int Quantity, decimal UnitPrice)> Updates { get; } = new();

        public List<SaleItem> NewItems { get; } = new();

        public HashSet<Guid> ReferencedIds { get; } = new();
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
