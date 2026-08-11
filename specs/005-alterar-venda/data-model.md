# Data Model: Alterar Venda

Esta feature estende o agregado `Sale` já modelado por `002-registrar-venda` com um segundo
método de escrita (`Update`, além de `Create`) e dois novos eventos de domínio. Nenhuma
entidade, tabela ou coluna nova é introduzida — ver
`specs/002-registrar-venda/data-model.md` para a definição completa dos campos existentes.

## Sale (aggregate root) — novo método `Update`

```text
Result<Sale> Update(
    ExternalReference customer,
    ExternalReference branch,
    DateTime? saleDate,
    IReadOnlyCollection<SaleItemChangeInput> items)
```

| Regra | Origem | Comportamento |
|---|---|---|
| Venda cancelada é imutável | INV-07 / FR-008 | Se `IsCancelled == true`, retorna `Failure` com chave `"sale"` antes de validar qualquer outra coisa. |
| Cliente e filial obrigatórios | FR-001 | Mesma validação de `Sale.Create` (`ValidateCustomer`/`ValidateBranch`), chaves `"customer"`/`"branch"`. |
| Data obrigatória | FR-002 (clarificação) | Se `saleDate` for `null`, `Failure` com chave `"saleDate"` — diferente do registro, onde a data é opcional. |
| Ao menos um item no corpo | INV-01 / FR-009 | Coleção `items` vazia ou nula → `Failure` com chave `"items"`. |
| Item existente é atualizado | FR-003 | `Id` presente e correspondente a um item **ativo** da venda → `SaleItem.ApplyChange(quantity, unitPrice)`. |
| Produto de item existente é imutável | FR-004 (clarificação) | `Id` presente cujo `Product.Id` do pedido difere do `Product.Id` atual do item → `Failure`, chave `"items[{i}].product.id"`. |
| `id` que não pertence à venda, ou que referencia item já cancelado | FR-010 (clarificação) | `Id` presente sem correspondência entre os itens **ativos** da venda → `Failure`, chave `"items[{i}].id"` (mensagem distingue "não encontrado" de "já cancelado"). |
| Item novo é adicionado | FR-005 | `Id` ausente → `SaleItem.Create(product, quantity, unitPrice)`, mesma validação do registro. |
| Item ausente do corpo é cancelado implicitamente | FR-006 | Todo item atualmente ativo cujo `Id` não aparece no corpo → `SaleItem.Cancel()`; gera um `ItemCancelled` por item. |
| Quantidade entre 1 e 20 | INV-02 / FR-011 | Validada tanto para itens atualizados quanto para novos, mesma mensagem de `SaleItem.Create`. |
| Produto único por venda | INV-03 / FR-012 | Verificado sobre o conjunto final de itens ativos (atualizados + novos), mesma lógica de `Sale.Create`. |
| Total sempre derivado | INV-05 / FR-014 | Desconto e total de cada item são sempre recalculados por `DiscountPolicy`; nenhum valor de desconto/total é aceito do pedido. |
| Total geral recalculado | INV-06 / FR-007 | `TotalAmount = Items.Where(i => !i.IsCancelled).Sum(i => i.TotalAmount)`, após a reconciliação completa. |
| Nenhuma mutação em caso de erro | FR-017 | Toda validação ocorre antes de qualquer mutação do agregado (ver `research.md`, seção 1). |
| Eventos | FR-015/FR-016 | Um `ItemCancelled` por item cancelado implicitamente, seguido de exatamente um `SaleModified`, ambos só quando `Result` é sucesso. |
| `UpdatedAt` | auditoria | Atualizado para `DateTime.UtcNow` em toda alteração bem-sucedida. |

## SaleItem (entidade do agregado) — novos métodos

```text
void ApplyChange(int quantity, decimal unitPrice)   // revalida INV-02, recalcula DiscountPercentage/DiscountAmount/TotalAmount
void Cancel()                                        // IsCancelled = true
```

Ambos só são chamados de dentro de `Sale.Update` (ver `research.md`, seção 4) — `Product` de um
`SaleItem` nunca é alterado por nenhum dos dois métodos, reforçando a imutabilidade de produto
(FR-004).

## `SaleItemChangeInput` (novo value object de entrada, Domain)

```text
record SaleItemChangeInput(Guid? Id, ExternalReference Product, int Quantity, decimal UnitPrice)
```

Análogo a `SaleItemInput` (usado por `Create`), acrescido de `Id` opcional para viabilizar a
reconciliação — presente identifica atualização, ausente identifica adição. Mesmo nome de campo
usado por `SaleItemResponse.Id` (consulta e resposta do próprio `PUT`), por consistência entre
leitura e escrita do mesmo recurso.

## Novos eventos de domínio

| Evento | Quando | Payload |
|---|---|---|
| `SaleModified` | Alteração bem-sucedida, sempre exatamente um por requisição | `SaleId`, `SaleNumber`, `TotalAmount`, `OccurredOn` |
| `ItemCancelled` | Um por item cancelado implicitamente pela reconciliação | `SaleId`, `SaleItemId`, `ProductId`, `Quantity`, `OccurredOn` |

Ambos herdam de `DomainEvent` (`SalesApi.Domain.Common`), acumulados via `AddDomainEvent` e
despachados por `AppDbContext.SaveChangesAsync` — mecanismo já genérico, sem alteração.

## ExternalReference (value object, reutilizado em Customer/Branch/Product)

Sem alteração em relação a `002-registrar-venda`. Usado para comparar a identidade do produto
de um item existente (`Product.Id`) na regra de imutabilidade (FR-004) — apenas o `Id` é
comparado; o `Name` denormalizado não participa dessa verificação.

## Transição de estado

Esta feature não introduz uma nova transição de nível de venda (`Ativa → Ativa`, conforme o
diagrama de ciclo de vida do Notion) — a venda permanece ativa após uma alteração bem-sucedida.
A única transição de nível de **item** é `Ativo → Cancelado`, aplicada implicitamente pela
reconciliação (não há transição inversa: item cancelado não pode voltar a ativo por esta
operação, FR-010).

## DTOs (Application)

Requisição (novos):

- `UpdateSaleRequest(DateTime? SaleDate, ExternalReferenceRequest Customer,
  ExternalReferenceRequest Branch, IReadOnlyCollection<SaleItemChangeRequest> Items)`
- `SaleItemChangeRequest(Guid? Id, ExternalReferenceRequest Product, int Quantity,
  decimal UnitPrice)`

Resposta (reaproveitados de `002-registrar-venda`/`003-consultar-venda`, sem alteração):

- `SaleResponse`, `SaleItemResponse`, `ExternalReferenceResponse`
