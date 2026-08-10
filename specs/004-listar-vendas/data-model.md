# Data Model: Listar Vendas

Esta feature não introduz nenhuma entidade, value object ou evento de domínio novo, nem altera
o schema de colunas existente. Ela expõe, em modo somente leitura e paginado, uma projeção
resumida do agregado `Sale` já modelado e persistido pela feature 002
(`002-registrar-venda`) — ver `specs/002-registrar-venda/data-model.md` para a definição
completa do agregado. O único ajuste de Infrastructure é a adição de três índices sobre colunas
já existentes (ver `research.md`, seção 5).

## Sale (aggregate root) — projeção resumida (`SaleSummaryResponse`)

| Campo | Tipo | Origem na consulta |
|---|---|---|
| `Id` | `Guid` | lido diretamente — também usado como critério de desempate na ordenação |
| `SaleNumber` | `string` | lido diretamente da linha persistida |
| `SaleDate` | `DateTime` (UTC) | lido diretamente — critério primário de ordenação (decrescente) |
| `Customer` | `ExternalReference` → `ExternalReferenceResponse` | lido diretamente (owned type); também usado como filtro (`customerId`) |
| `Branch` | `ExternalReference` → `ExternalReferenceResponse` | lido diretamente (owned type); também usado como filtro (`branchId`) |
| `TotalAmount` | `decimal` | lido diretamente — **não** recalculado nesta feature (FR-014) |
| `IsCancelled` | `bool` | lido diretamente; também usado como filtro (`isCancelled`) |

**Deliberadamente omitido**: `Items` (FR-004) — a listagem nunca carrega nem retorna a coleção
de itens de cada venda; para isso existe a consulta individual (`003-consultar-venda`).

## Página de resultados (`PagedResult<SaleSummaryResponse>`)

Não é uma entidade de domínio — é um DTO de aplicação genérico (`SalesApi.Application.Common.Dtos`),
reutilizável por qualquer listagem paginada futura no projeto.

| Campo | Tipo | Descrição |
|---|---|---|
| `Items` | `IReadOnlyCollection<T>` | vendas resumidas da página atual |
| `Page` | `int` | página atual (eco do parâmetro validado, após aplicar o padrão 1) |
| `PageSize` | `int` | tamanho de página aplicado (eco do parâmetro validado, após aplicar o padrão 20) |
| `TotalCount` | `int` | total de vendas que atendem aos filtros informados, independente de paginação |
| `TotalPages` | `int` | `0` quando `TotalCount = 0`; caso contrário, `ceil(TotalCount / PageSize)` |

## Filtro de listagem (parâmetros de `ListSalesQuery`, não persistido)

Objeto de parâmetro da Application, não uma entidade — representa a intenção de consulta
recebida via query string, antes de validação:

| Parâmetro | Tipo bruto | Validação aplicada pelo handler |
|---|---|---|
| `page` | `string?` | opcional; padrão `1`; deve converter para inteiro ≥ 1 |
| `pageSize` | `string?` | opcional; padrão `20`; deve converter para inteiro entre 1 e 100 |
| `customerId` | `string?` | opcional; quando informado, deve converter para `Guid` |
| `branchId` | `string?` | opcional; quando informado, deve converter para `Guid` |
| `isCancelled` | `string?` | opcional; quando informado, deve converter para `bool` (`true`/`false`) |

Cada falha de conversão ou de faixa gera um `Notification` próprio (`key` = nome do parâmetro),
todos acumulados antes de retornar `Result.Failure` — mesmo padrão de acumulação de erros já
usado por `Sale.Create` (ver `research.md`, seção 1).

## Sem transição de estado

Esta feature não altera o ciclo de vida de nenhuma venda — nenhuma transição é aplicada. O único
efeito colateral permitido é a leitura; nenhuma escrita, nenhum evento de domínio (FR-013,
FR-015).

## Índices adicionados (Infrastructure)

| Índice | Coluna(s) | Motivo |
|---|---|---|
| `ix_sales_customer_id` | `sales.customer_id` | suporta o filtro `customerId` (FR-006) |
| `ix_sales_branch_id` | `sales.branch_id` | suporta o filtro `branchId` (FR-007) |
| `ix_sales_sale_date` | `sales.sale_date` | suporta a ordenação padrão por data decrescente (FR-003) |

Nenhum índice novo é necessário para `isCancelled` (baixa cardinalidade, filtro booleano) nem
para o desempate por `Id` (já é chave primária, portanto já indexado).

## DTOs novos (Application)

- `SaleSummaryResponse` (`SalesApi.Application.Sales.Dtos`) — projeção resumida descrita acima.
- `PagedResult<T>` (`SalesApi.Application.Common.Dtos`) — envelope de paginação genérico
  descrito acima.

Nenhum DTO existente (`SaleResponse`, `SaleItemResponse`, `ExternalReferenceResponse`) é
alterado; `ExternalReferenceResponse` é reaproveitado dentro de `SaleSummaryResponse.Customer`
e `.Branch`.
