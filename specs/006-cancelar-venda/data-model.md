# Data Model: Cancelar Venda

Esta feature estende o agregado `Sale` já modelado por `002-registrar-venda` e ajustado por
`005-alterar-venda` com um terceiro método de escrita (`Cancel`, além de `Create` e `Update`) e
um novo evento de domínio. Nenhuma entidade, tabela ou coluna de negócio nova é introduzida — ver
`specs/002-registrar-venda/data-model.md` para a definição completa dos campos existentes.

## Sale (aggregate root) — novo método `Cancel`

```text
Result<Sale> Cancel()
```

| Regra | Origem | Comportamento |
|---|---|---|
| Venda cancelada é imutável | INV-07 / FR-005 | Se `IsCancelled == true`, retorna `Failure` com chave `"sale"` imediatamente, sem mutar nada. |
| Cancelar todo item ainda ativo | FR-002 | Para cada item em `Items.Where(i => !i.IsCancelled)`, chama `SaleItem.Cancel()`. |
| Itens já cancelados individualmente permanecem inalterados | FR-012 | Itens com `IsCancelled == true` antes da chamada não sofrem nenhuma mutação nem geram efeito colateral. |
| Total geral zerado | INV-06 / FR-003 | `TotalAmount = 0m` após cancelar todos os itens ainda ativos (nenhum item ativo resta, então a soma dos totais ativos é sempre zero). |
| Cancelamento é sempre lógico | FR-004 | Nenhuma linha é removida de `sales` nem de `sale_items` — apenas `is_cancelled`/`total_amount` mudam. |
| Evento único | FR-008 / FR-009 | Um único `SaleCancelled` é registrado; nenhum `ItemCancelled` é emitido pelo cancelamento em massa. |
| `UpdatedAt` | auditoria | Atualizado para `DateTime.UtcNow` no cancelamento bem-sucedido. |
| Nenhuma mutação em caso de erro | FR-010 | A única validação (`IsCancelled`) ocorre antes de qualquer mutação — não há necessidade do padrão two-pass de `Sale.Update` (ver `research.md`, seção 1). |

## SaleItem (entidade do agregado) — sem novos métodos

Reaproveita `SaleItem.Cancel()`, já introduzido por `005-alterar-venda`, sem nenhuma alteração de
assinatura ou comportamento. Continua só sendo chamado de dentro de `Sale` (Princípio I).

## Novo evento de domínio

| Evento | Quando | Payload |
|---|---|---|
| `SaleCancelled` | Cancelamento da venda inteira bem-sucedido | `SaleId`, `SaleNumber`, `OccurredOn` |

Herda de `DomainEvent` (`SalesApi.Domain.Common`), acumulado via `AddDomainEvent` e despachado
por `AppDbContext.SaveChangesAsync` — mecanismo já genérico, sem alteração.

## Transição de estado

Esta feature implementa a transição `Ativa → Cancelada` por `DELETE` explícito, já prevista no
diagrama de ciclo de vida do Notion (`Ativa --> Cancelada : DELETE - SaleCancelled`). É a
contraparte explícita da transição implícita já coberta por `005-alterar-venda`
(cancelamento de todos os itens via reconciliação não é o mesmo fluxo — aquele permanece fora do
escopo desta feature, ver `spec.md`, seção Assumptions). `Cancelada` é estado terminal: nenhuma
transição de saída existe.

## Concorrência (Infrastructure, não é conceito de Domain)

`SaleConfiguration` passa a mapear o `xmin` de sistema do PostgreSQL como token de concorrência
do EF Core (`builder.Property<uint>("xmin").IsRowVersion()`), afetando toda escrita em `Sale` —
não apenas `Cancel()`. Ver `research.md`, seções 3 e 4, para a decisão completa e o efeito colateral em
`UpdateSaleCommandHandler` (005). Isso não introduz nenhum campo novo em `Sale` nem em nenhum
DTO de resposta — é inteiramente transparente ao Domain e à Application, visível apenas como uma
`DbUpdateConcurrencyException` possível ao redor de `SaveChangesAsync`.

## DTOs (Application)

Nenhum DTO novo. O comando de entrada (`CancelSaleCommand`) carrega apenas o `Id` já extraído da
rota pelo endpoint — não há corpo de requisição (ver `contracts/cancel-sale.md`). A resposta de
sucesso não tem corpo (`204`), então nenhum DTO de resposta é necessário; `SaleResponse` e
`SaleItemResponse` (reaproveitados de `002`/`003`) continuam a única forma de representar a venda
para quem quiser confirmar o cancelamento via `GET /api/sales/{id}`.
