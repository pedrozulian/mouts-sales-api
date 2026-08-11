# Data Model: Cancelar Item da Venda

Esta feature estende o agregado `Sale` já modelado por `002-registrar-venda` e ajustado por
`005-alterar-venda` e `006-cancelar-venda` com um quarto método de escrita (`CancelItem`, além de
`Create`, `Update` e `Cancel`). Nenhuma entidade, tabela ou coluna de negócio nova é introduzida —
ver `specs/002-registrar-venda/data-model.md` para a definição completa dos campos existentes.

## Sale (aggregate root) — novo método `CancelItem`

```text
Result<Sale> CancelItem(Guid itemId)
```

| Regra | Origem | Comportamento |
|---|---|---|
| Venda cancelada é imutável | INV-07 / FR-005 | Se `IsCancelled == true`, retorna `Failure` com chave `"sale"` imediatamente, sem localizar o item nem mutar nada. |
| Item deve pertencer à venda | FR-006 / FR-007 | Se nenhum item de `Items` tem `Id == itemId`, retorna `Failure` com chave `"itemId"`. Cobre tanto identificador inexistente quanto item de outra venda — a busca é sempre restrita a `_items` do agregado carregado. |
| Item já cancelado não pode ser cancelado de novo | INV-08 / FR-008 | Se o item encontrado já tem `IsCancelled == true`, retorna `Failure` com chave `"item"`, sem mutar nada nem emitir evento. |
| Cancelar apenas o item indicado | FR-002 | Chama `SaleItem.Cancel()` (já existente desde 005) somente no item alvo — os demais itens não são tocados. |
| Total recalculado a partir dos itens ativos | INV-06 / FR-003 | `TotalAmount = Items.Where(i => !i.IsCancelled).Sum(i => i.TotalAmount)` após cancelar o item alvo. |
| Cancelamento é sempre lógico | FR-004 | Nenhuma linha é removida de `sales` nem de `sale_items` — apenas `is_cancelled`/`total_amount` mudam. |
| Evento de item | FR-011 | Um `ItemCancelled` (já existente desde 005) é sempre registrado após um cancelamento de item bem-sucedido. |
| Cascata: último item ativo cancela a venda | INV-09 / FR-009 | Se, após cancelar o item alvo, nenhum item de `Items` permanece ativo, delega para `Cancel()` (já existente desde 006) — que marca `IsCancelled = true`, zera `TotalAmount` (já era zero) e registra `SaleCancelled`. Ver `research.md`, seção 1, para a decisão de reaproveitar `Cancel()` em vez de duplicar a lógica. |
| `UpdatedAt` | auditoria | Atualizado para `DateTime.UtcNow` no cancelamento do item; reafirmado por `Cancel()` quando a cascata se aplica. |
| Nenhuma mutação em caso de erro | FR-013 | As três validações (venda cancelada, item não encontrado, item já cancelado) ocorrem antes de qualquer mutação — mesmo padrão de passagem única de `Cancel()` (ver `specs/006-cancelar-venda/research.md`, seção 1). |

## SaleItem (entidade do agregado) — sem novos métodos

Reaproveita `SaleItem.Cancel()`, já introduzido por `005-alterar-venda`, sem nenhuma alteração de
assinatura ou comportamento. Continua só sendo chamado de dentro de `Sale` (Princípio I).

## Eventos de domínio — nenhum evento novo

| Evento | Quando (nesta feature) | Payload |
|---|---|---|
| `ItemCancelled` (já existe, 005) | Cancelamento de item bem-sucedido, sempre | `SaleId`, `SaleItemId`, `ProductId`, `Quantity`, `OccurredOn` |
| `SaleCancelled` (já existe, 006) | Cancelamento de item bem-sucedido que esgota os itens ativos da venda (cascata) | `SaleId`, `SaleNumber`, `OccurredOn` |

Ambos herdam de `DomainEvent` (`SalesApi.Domain.Common`), acumulados via `AddDomainEvent` e
despachados por `AppDbContext.SaveChangesAsync` — mecanismo já genérico, sem alteração. Quando a
cascata se aplica, os dois eventos são despachados juntos, após a mesma transação bem-sucedida —
nunca em chamadas separadas.

## Transição de estado

Esta feature implementa duas transições já previstas no diagrama de ciclo de vida do Notion:

- `Ativa → Ativa` (cancelar item, restam outros ativos — `ItemCancelled`): cobre a User Story 1.
- `Ativa → Cancelada` (cancelar o último item ativo — `ItemCancelled` + `SaleCancelled`): cobre a
  User Story 2, reaproveitando a mesma transição de estado terminal já implementada por
  `006-cancelar-venda` para o `DELETE` explícito da venda inteira.

`Cancelada` continua sendo estado terminal: nenhuma transição de saída existe, e `CancelItem`
rejeita qualquer tentativa de operar sobre uma venda já nesse estado (INV-07).

## Concorrência (Infrastructure, não é conceito de Domain) — reaproveitada, sem mudança

`SaleConfiguration` já mapeia o `xmin` de sistema do PostgreSQL como token de concorrência do EF
Core desde a feature 006 (`builder.Property<uint>("xmin").IsRowVersion()`), afetando toda escrita
em `Sale` — incluindo as originadas por `CancelItem`, que sempre grava `TotalAmount`/`UpdatedAt`
na linha de `Sale`. Ver `research.md`, seção 4, para a decisão completa. Isso não introduz nenhum
campo novo em `Sale`, em `SaleItem` nem em nenhum DTO de resposta.

## DTOs (Application)

Nenhum DTO novo. O comando de entrada (`CancelSaleItemCommand`) carrega apenas `SaleId` e `ItemId`,
ambos extraídos da rota pelo endpoint — não há corpo de requisição (ver
`contracts/cancel-sale-item.md`). A resposta de sucesso não tem corpo (`204`), então nenhum DTO de
resposta é necessário; `SaleResponse` e `SaleItemResponse` (reaproveitados de `002`/`003`)
continuam a única forma de representar a venda para quem quiser confirmar o cancelamento via
`GET /api/sales/{id}`.
