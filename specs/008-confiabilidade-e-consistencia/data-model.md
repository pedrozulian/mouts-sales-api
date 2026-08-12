# Data Model: Confiabilidade Operacional e Consistência de Dados

Esta feature não introduz entidade, tabela ou coluna de negócio nova — ver
`specs/002-registrar-venda/data-model.md` para a definição completa dos campos existentes do
agregado `Sale`. O que muda é: (a) como dois campos derivados já existentes são calculados
(arredondamento), (b) uma regra de validação já existente que passa a considerar mais estado
(itens cancelados), e (c) o nome físico das colunas que já existem.

## Sale (aggregate root) — sem novo campo, `TotalAmount` passa a somar valores já arredondados

| Campo | Mudança nesta feature |
|---|---|
| `TotalAmount` | Continua derivado (soma dos totais dos itens ativos). Como cada `SaleItem.TotalAmount` agora chega já arredondado em duas casas, a soma nunca produz um valor com mais de duas casas — sem necessidade de arredondar a soma em si (FR-011). |

Nenhuma outra propriedade de `Sale` muda de tipo, nome ou regra nesta feature.

## SaleItem (entidade do agregado) — arredondamento no cálculo, novo método de validação reaproveitado

| Campo | Mudança nesta feature |
|---|---|
| `DiscountAmount` | Passa a ser calculado como `Math.Round(grossAmount * DiscountPercentage, 2, MidpointRounding.AwayFromZero)` — antes, sem arredondamento (FR-008/FR-009/FR-010). |
| `TotalAmount` | Passa a ser calculado como `grossAmount - DiscountAmount`, onde `DiscountAmount` já está arredondado — o subtraendo, não o resultado da subtração, é o valor arredondado (garante que a soma de itens sempre bata com o total da venda, conforme documentado no Domain Model do Notion). |

**Novo membro**: `internal static IReadOnlyCollection<Notification> ValidateChange(int quantity,
decimal unitPrice)` — extrai as três validações hoje duplicadas entre `SaleItem.Create` e
`Sale.ReconcileExistingItem` (quantidade entre 1 e 20, preço unitário maior que zero), devolvendo
as mesmas mensagens e chaves já usadas nos dois pontos. `SaleItem.Create` e
`Sale.ReconcileExistingItem` passam a chamá-lo em vez de repetir os `if`s (US7, ver research.md
seção 7). Não é um método público de negócio novo — é uma extração interna, sem efeito
observável.

## Regra de reconciliação de itens (`Sale.Update` / `ReconcileNewItem`) — escopo de INV-03 corrigido

| Antes desta feature | Depois desta feature |
|---|---|
| Um item novo (sem `id`) só é rejeitado por produto duplicado se o mesmo produto aparecer em outro item **do corpo da requisição atual**. Um produto que já existe na venda como item **cancelado** não é detectado pelo domínio — a tentativa de inserir a linha nova é rejeitada pelo banco (violação do índice único `(sale_id, product_id)`), e a exceção de infraestrutura escapa sem tradução. | Um item novo é rejeitado quando o produto já pertence à venda em **qualquer estado** — ativo ou cancelado —, com `Notification` na chave `items[{index}].product.id` e mensagem "Produto já pertence a esta venda.". A tentativa nunca chega ao `SaveChanges` (FR-017/FR-018/FR-019, US4). |

O índice único `uq_sale_product` (renomeado nesta feature, ver seção seguinte) permanece
exatamente com o mesmo efeito de antes — ele já cobria todos os estados; o que muda é o domínio
passar a antecipar essa rejeição.

## Modelo físico — renomeação, sem mudança de tipo ou de dado

Todas as colunas abaixo já existem; apenas o nome físico muda, via nova migration
(`RenameColumnsToSnakeCase`) sobre o schema criado por `CreateSales`/`AddSalesListIndexes`.

### `sales`

| Coluna atual | Coluna após esta feature |
|---|---|
| `Id` | `id` |
| `SaleNumber` | `sale_number` |
| `SaleDate` | `sale_date` |
| `customer_id` | `customer_id` *(sem mudança — já correto)* |
| `customer_name` | `customer_name` *(sem mudança — já correto)* |
| `branch_id` | `branch_id` *(sem mudança — já correto)* |
| `branch_name` | `branch_name` *(sem mudança — já correto)* |
| `TotalAmount` | `total_amount` |
| `IsCancelled` | `is_cancelled` |
| `CreatedAt` | `created_at` |
| `UpdatedAt` | `updated_at` |

Índice `IX_sales_SaleNumber` → `ix_sales_sale_number`. Índices `ix_sales_customer_id`,
`ix_sales_branch_id` e `ix_sales_sale_date` permanecem sem mudança (já em snake_case).

### `sale_items`

| Coluna atual | Coluna após esta feature |
|---|---|
| `Id` | `id` |
| `product_id` | `product_id` *(sem mudança — já correto)* |
| `product_name` | `product_name` *(sem mudança — já correto)* |
| `Quantity` | `quantity` |
| `UnitPrice` | `unit_price` |
| `DiscountPercentage` | `discount_percentage` |
| `DiscountAmount` | `discount_amount` |
| `TotalAmount` | `total_amount` |
| `IsCancelled` | `is_cancelled` |
| `SaleId` (shadow property, FK) | `sale_id` |

Índice `IX_sale_items_SaleId` → `ix_sale_items_sale_id`. Constraint única
`IX_sale_items_SaleId_product_id` → `uq_sale_product` (nome alinhado ao Domain Model do Notion).

### `sale_number_seq`

Sem mudança — já é um identificador de sequence independente de convenção de coluna.

## Health check — novo tipo de diagnóstico, sem novo campo de resposta

`PendingMigrationsHealthCheck` reaproveita o mesmo formato de resposta já documentado em
`specs/001-project-setup/contracts/health-check.md` (`status`, `checks[].name`,
`checks[].status`, `checks[].description`) — apenas passa a alimentar `checks[0].status` com um
diagnóstico mais preciso (schema desatualizado vs. apenas conectividade). Nenhum campo novo é
adicionado ao contrato (FR-007). Ver `contracts/health-check.md` desta feature para o contrato
atualizado.

## Contrato de erro — novo tipo de causa, mesmo formato

O envelope `{ "errors": [{ "key": string, "message": string }] }`, já usado por toda rejeição de
regra de negócio e recurso não encontrado, passa a cobrir também falhas inesperadas — com a chave
fixa `"server"` e uma mensagem genérica, nunca o detalhe da exceção original (FR-013/FR-014). Ver
`contracts/error-contract.md`.

## DTOs (Application)

Nenhum DTO novo, nenhum campo novo em DTO existente. `SaleItemResponse.DiscountAmount` e
`SaleItemResponse.TotalAmount` (e os equivalentes em `SaleResponse.TotalAmount`) passam a conter
sempre valores com no máximo duas casas decimais — mudança de valor, não de forma.
