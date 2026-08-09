# Data Model: Registrar Venda

Extraído da spec (`spec.md`) e da documentação DDD (Notion — página "Domain Model"),
restrito ao necessário para o caso de uso de criação (User Stories 1, 2 e 3). Comportamentos
de alteração e cancelamento (`Cancel`, `ChangeItem`, `CancelItem`) pertencem a features
futuras (UC-04, UC-05, UC-06) e não são expostos publicamente pelo agregado nesta versão.

## Sale (aggregate root)

| Campo | Tipo | Regra | Origem |
|---|---|---|---|
| `Id` | `Guid` | gerado internamente | — |
| `SaleNumber` | `string` | gerado pelo sistema, único (INV-10), nunca aceito na requisição (FR-009) | sequence do banco |
| `SaleDate` | `DateTime` (UTC) | opcional na requisição; default = momento do registro (FR-011) | requisição ou `DateTime.UtcNow` |
| `Customer` | `ExternalReference` | obrigatório, `Id` e `Name` não vazios (FR-007) | requisição |
| `Branch` | `ExternalReference` | obrigatório, `Id` e `Name` não vazios (FR-007) | requisição |
| `Items` | `IReadOnlyCollection<SaleItem>` | ao menos 1 item (INV-01, FR-004) | requisição, um `SaleItem` por entrada |
| `TotalAmount` | `decimal` | derivado — soma dos totais dos itens (FR-008); nunca aceito na requisição (FR-010) | calculado |
| `IsCancelled` | `bool` | sempre `false` na criação | — |
| `CreatedAt` / `UpdatedAt` | `DateTime` (UTC) | auditoria, preenchidos no momento do registro | — |

**Invariantes aplicadas nesta feature**: INV-01, INV-02, INV-03, INV-04, INV-05, INV-10 (ver
`spec.md`, FR-002 a FR-010).

## SaleItem (entidade do agregado)

| Campo | Tipo | Regra | Origem |
|---|---|---|---|
| `Id` | `Guid` | gerado internamente | — |
| `Product` | `ExternalReference` | obrigatório, `Id` e `Name` não vazios (FR-007) | requisição |
| `Quantity` | `int` | entre 1 e 20 (INV-02, FR-003); produto não pode repetir entre itens (INV-03, FR-005) | requisição |
| `UnitPrice` | `decimal` | maior que zero (INV-04, FR-006) | requisição |
| `DiscountPercentage` | `decimal` | derivado de `Quantity` via `DiscountPolicy` (FR-002) | calculado |
| `DiscountAmount` | `decimal` | derivado: `UnitPrice × Quantity × DiscountPercentage` | calculado |
| `TotalAmount` | `decimal` | derivado: bruto − desconto (FR-008) | calculado |
| `IsCancelled` | `bool` | sempre `false` na criação | — |

## ExternalReference (value object, reutilizado em Customer/Branch/Product)

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | `Guid` | obrigatório, não vazio |
| `Name` | `string` | obrigatório, não vazio |

Sem identidade própria, sem ciclo de vida — `record` imutável (igualdade por valor).

## DiscountPolicy (regra de domínio, não é entidade)

| Faixa de `Quantity` | `DiscountPercentage` |
|---|---|
| 1 – 3 | 0% |
| 4 – 9 | 10% |
| 10 – 20 | 20% |
| > 20 | rejeitado antes de chegar aqui (INV-02) |

## SaleCreated (evento de domínio)

| Campo | Origem |
|---|---|
| `SaleId` | `Sale.Id` |
| `SaleNumber` | `Sale.SaleNumber` |
| `CustomerId` | `Sale.Customer.Id` |
| `BranchId` | `Sale.Branch.Id` |
| `TotalAmount` | `Sale.TotalAmount` |
| `OccurredOn` | herdado de `DomainEvent` |

Emitido apenas quando `Sale.Create` retorna sucesso e a transação é persistida (User Story 3,
FR-015).

## Transições de estado (escopo desta feature)

```text
[inexistente] --Create bem-sucedido--> Ativa
[inexistente] --Create rejeitado (invariante violada)--> nada é persistido
```

Os demais estados do ciclo de vida completo (Ativa → Cancelada, cancelamento de item) estão
documentados na página "Domain Model" do Notion e serão cobertos pelas features de UC-04,
UC-05 e UC-06.
