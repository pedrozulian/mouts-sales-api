# Implementation Plan: Listar Vendas

**Branch**: `004-listar-vendas` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-listar-vendas/spec.md`

## Summary

Listar vendas (`GET /api/sales`): retorna uma página de vendas em forma resumida (sem
`Items`), ordenada por `SaleDate` decrescente com desempate por `Id`, com filtros opcionais
`customerId`, `branchId` e `isCancelled`, e metadados de paginação (`page`, `pageSize`,
`totalCount`, `totalPages`). Abordagem: query MediatR (`ListSalesQuery`) na camada Application
recebendo os parâmetros de query string como `string?` brutos, validando e convertendo cada um
manualmente (`Result`/`Notification`) para que todo erro de parâmetro malformado — não só os de
faixa de paginação — passe pelo mesmo contrato de erro da API. A consulta usa
`IApplicationDbContext.Sales.AsNoTracking()` com filtros condicionais, `CountAsync` para o
total e `Skip`/`Take` seguidos de projeção Mapster (`ProjectToType<SaleSummaryResponse>`) para
que o SQL gerado nunca carregue a coleção `Items`. Endpoint Minimal API na camada Api traduz
`Result` para `200`/`400`. Diferente de `003-consultar-venda`, esta feature também ajusta a
Infrastructure: adiciona índices em `sales(customer_id)`, `sales(branch_id)` e
`sales(sale_date)` — já previstos no modelo de persistência da documentação DDG do Notion, mas
ainda não criados pela migration de `002-registrar-venda` — via uma nova migration EF Core. O
Domain não é alterado.

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS)

**Primary Dependencies**: ASP.NET Core Minimal APIs (endpoint adicional em
`SalesApi.Api.Sales.SalesEndpoints`), MediatR 14.2.0 (query e handler), Mapster 10.0.11
(`ProjectToType<SaleSummaryResponse>` sobre `IQueryable<Sale>`, novo `ListSalesMappingConfig`),
Entity Framework Core 8 via `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11 (leitura via
`IApplicationDbContext.Sales`, nova migration de índices), Serilog.AspNetCore (log estruturado
no handler). Nenhuma dependência nova é adicionada ao projeto.

**Storage**: PostgreSQL 16. Reaproveita as tabelas `sales`/`sale_items` já criadas pela
migration `CreateSales` (002-registrar-venda); adiciona uma nova migration
(`AddSalesListIndexes`) criando `ix_sales_customer_id`, `ix_sales_branch_id` e
`ix_sales_sale_date` sobre `sales`, conforme já especificado na seção "Modelo de persistência"
da documentação DDD do Notion e ainda não materializado no código.

**Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) para
o teste de ponta a ponta do endpoint; `coverlet.collector` para cobertura. Testes de filtro por
`isCancelled=true` preparam esse estado diretamente no banco (mesmo padrão de
`003-consultar-venda`, `research.md` seção 6), já que UC-05/UC-06 ainda não existem. Testes de
paginação e ordenação (inclusive o desempate por `Id` em datas iguais) usam o comando de
registro já existente (`CreateSaleCommand`), informando `saleDate` explícito.

**Target Platform**: serviço web ASP.NET Core containerizado (Linux, Docker), execução local
via `docker compose` ou `dotnet run` — mesmo ambiente já orquestrado pela feature 002.

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já
existentes: Domain, Application, Infrastructure, Api); esta feature adiciona código em
Application, Api e Infrastructure (apenas configuração de índice + migration).

**Performance Goals**: sem meta formal de throughput — protótipo de avaliação técnica com uso
local/single-instance. A paginação é resolvida no banco (`Skip`/`Take` + `CountAsync`), nunca
carregando a tabela inteira em memória, e os novos índices evitam full table scan nos filtros
mais comuns (cliente, filial, data).

**Constraints**: cobertura mínima de 90% (Princípio IX); listagem não pode recalcular
desconto/total nem revalidar regras de registro (FR-014); nenhum evento de domínio disparado
(FR-015); nenhuma alteração de estado (FR-013); toda resposta de erro — inclusive parâmetro em
formato inválido — segue o mesmo contrato `{ "errors": [...] }` (FR-012).

**Scale/Scope**: um único endpoint (`GET /api/sales`); volume de dados de teste, sem carga
concorrente real.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | Nenhuma lógica de negócio nova no Domain; a listagem apenas projeta o agregado `Sale` já validado e persistido pela feature 002, sem recalcular desconto/total (FR-014). | PASS |
| II. TDD | Tasks (a gerar via `/speckit-tasks`) seguem Red-Green-Refactor: testes de `ListSalesQueryHandler` (paginação, filtros, validação, desempate) e do endpoint escritos antes da implementação. | PASS |
| III. SOLID | `ListSalesQueryHandler` com responsabilidade única (validar parâmetros, consultar, paginar, mapear); `IApplicationDbContext` injetado por interface via construtor; `PagedResult<T>` genérico evita duplicar a forma de paginação em listagens futuras (OCP). | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md` e o contrato estão em português; identificadores de código permanecem em inglês. | PASS |
| V. Clean Architecture | Código novo em Application (`Sales/List/`, `Common/Dtos/PagedResult.cs`), Api (extensão de `SalesEndpoints`) e Infrastructure (índice + migration, sem lógica); Domain não é alterado. | PASS |
| VI. Eventos via Mediator | Não se aplica a eventos: listagem não dispara nenhum evento de domínio (FR-015). MediatR é usado apenas como Query/Handler. | PASS (N/A eventos) |
| VII. Result/Notification | Parâmetros inválidos (`page`, `pageSize`, `customerId`, `branchId`, `isCancelled`) são comunicados via `Result<PagedResult<SaleSummaryResponse>>.Failure(Notification[])`, nunca exception; endpoint traduz para `400`. Ver `research.md` seção 2 para o motivo de validar manualmente em vez de depender do binding nativo do framework. | PASS |
| VIII. Observabilidade | Logging estruturado explícito no `ListSalesQueryHandler` (parâmetros aplicados, total de registros encontrados), já que não há evento de domínio para carregar o log. | PASS |
| IX. Qualidade de Código | Tasks incluem testes suficientes (paginação, filtros isolados e combinados, validação, desempate) para manter o gate de 90% de cobertura verificado no CI (SonarCloud). | PASS |
| X. Docker | Nenhum novo recurso de infraestrutura — a nova migration de índices roda sobre o PostgreSQL já orquestrado pelo `docker-compose.yml` existente. | PASS |

Nenhuma violação identificada. `Complexity Tracking` não se aplica.

**Reavaliação pós Fase 1**: `data-model.md` não introduziu nenhuma entidade nova nem coluna
nova (apenas índices sobre colunas existentes e dois DTOs de leitura); `contracts/list-sales.md`
não introduziu nenhum campo fora do já modelado pelo Domain Model do Notion. Gate permanece
PASS.

## Project Structure

### Documentation (this feature)

```text
specs/004-listar-vendas/
├── plan.md                    # Este arquivo (/speckit-plan)
├── research.md                # Fase 0 (/speckit-plan)
├── data-model.md              # Fase 1 (/speckit-plan)
├── quickstart.md              # Fase 1 (/speckit-plan)
├── contracts/
│   └── list-sales.md          # Fase 1 (/speckit-plan)
├── checklists/
│   └── requirements.md
└── tasks.md                   # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/                          # SEM ALTERAÇÃO — reaproveita Sale/SaleItem/ExternalReference já existentes
│
├── SalesApi.Application/
│   ├── Common/
│   │   └── Dtos/
│   │       └── PagedResult.cs                 # NOVO — genérico, reutilizável por futuras listagens
│   └── Sales/
│       ├── Create/                            # já existe
│       ├── Get/                                # já existe
│       ├── Dtos/
│       │   └── SaleSummaryResponse.cs         # NOVO — forma resumida sem Items
│       └── List/                               # NOVO
│           ├── ListSalesQuery.cs               # IRequest<Result<PagedResult<SaleSummaryResponse>>>; page/pageSize/customerId/branchId/isCancelled como string?
│           ├── ListSalesQueryHandler.cs        # parse + validação manual, filtro condicional, Count + Skip/Take, ProjectToType, log estruturado
│           └── ListSalesMappingConfig.cs        # Sale -> SaleSummaryResponse (IRegister)
│
├── SalesApi.Infrastructure/
│   └── Persistence/
│       ├── Configurations/
│       │   └── SaleConfiguration.cs            # AJUSTAR — adicionar HasIndex(customer_id), HasIndex(branch_id), HasIndex(sale_date)
│       └── Migrations/
│           └── <timestamp>_AddSalesListIndexes.cs   # NOVA migration
│
└── SalesApi.Api/
    └── Sales/
        └── SalesEndpoints.cs                   # ajustar: adicionar MapGet("/api/sales", ListSales)

tests/
├── SalesApi.Application.Tests/Sales/
│   └── ListSalesQueryHandlerTests.cs           # paginação padrão, filtros isolados/combinados, isCancelled ausente vs. true/false, lista vazia, parâmetros inválidos (page/pageSize/customerId/branchId/isCancelled), desempate por Id
└── SalesApi.Api.Tests/Sales/
    └── ListSalesEndpointTests.cs               # WebApplicationFactory, 200 e 400, Postgres local (mesmo padrão de AppDbContextConnectionTests)
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida (4
projetos), sem criar nenhum projeto novo. Assim como `003-consultar-venda`, o Domain não é
tocado. Diferente dela, esta feature também ajusta a Infrastructure — mas apenas configuração
de índice e uma migration, sem nova lógica de persistência — porque é a primeira feature a de
fato consultar `sales` por `customer_id`, `branch_id` e `sale_date` em volume, motivo pelo qual
o modelo de persistência do Notion já previa esses índices. O código de aplicação novo segue o
mesmo padrão de pastas por caso de uso dentro de `Sales/` (`List/`, ao lado de `Create/` e
`Get/`), e `PagedResult<T>` fica em `Common/Dtos` por não ser específico de vendas — qualquer
listagem paginada futura no projeto pode reaproveitá-lo.

## Complexity Tracking

*Sem violações — seção não se aplica a este plano.*
