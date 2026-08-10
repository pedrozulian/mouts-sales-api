# Data Model: Consultar Venda

Esta feature não introduz nenhuma entidade, value object ou evento de domínio novo. Ela expõe,
em modo somente leitura, o agregado `Sale` já modelado e persistido pela feature 002
(`002-registrar-venda`) — ver `specs/002-registrar-venda/data-model.md` para a definição
completa de campos e invariantes de escrita.

O que muda aqui é apenas **como** os dados existentes são lidos e apresentados, não a forma do
agregado em si.

## Sale (aggregate root) — projeção de leitura

| Campo | Tipo | Origem na consulta |
|---|---|---|
| `Id` | `Guid` | filtro da query (parâmetro de rota) |
| `SaleNumber` | `string` | lido diretamente da linha persistida |
| `SaleDate` | `DateTime` (UTC) | lido diretamente |
| `Customer` | `ExternalReference` | lido diretamente (owned type) |
| `Branch` | `ExternalReference` | lido diretamente (owned type) |
| `TotalAmount` | `decimal` | lido diretamente — **não** recalculado nesta feature (FR-010) |
| `IsCancelled` | `bool` | lido diretamente — vendas canceladas são retornadas normalmente (FR-005) |
| `Items` | `IReadOnlyCollection<SaleItem>` | carregado via `Include`, ativos e cancelados (FR-006) |

## SaleItem (entidade do agregado) — projeção de leitura

| Campo | Tipo | Origem na consulta |
|---|---|---|
| `Id` | `Guid` | lido diretamente |
| `Product` | `ExternalReference` | lido diretamente (owned type) |
| `Quantity` | `int` | lido diretamente |
| `UnitPrice` | `decimal` | lido diretamente |
| `DiscountPercentage` | `decimal` | lido diretamente — **não** recalculado (FR-010) |
| `DiscountAmount` | `decimal` | lido diretamente |
| `TotalAmount` | `decimal` | lido diretamente |
| `IsCancelled` | `bool` | lido diretamente, independente por item (FR-003, FR-006) |

## ExternalReference (value object, reutilizado em Customer/Branch/Product)

Sem alteração em relação a `002-registrar-venda`: `record` imutável com `Id` e `Name`.

## Sem transição de estado

Esta feature não altera o ciclo de vida da venda — nenhuma transição é aplicada. O único
efeito colateral permitido é a leitura; nenhuma escrita, nenhum evento de domínio (FR-009,
FR-011).

## DTOs reaproveitados (Application)

Nenhum DTO novo. A resposta é montada com os tipos já existentes em
`SalesApi.Application.Sales.Dtos`:

- `SaleResponse` (já inclui todos os campos acima, inclusive `IsCancelled` e `Items`)
- `SaleItemResponse` (já inclui `IsCancelled` por item)
- `ExternalReferenceResponse`

Ver `specs/002-registrar-venda/data-model.md` para a origem desses tipos e
`research.md` (seção 3) desta feature para a decisão de reaproveitar o mapeamento Mapster já
registrado.
