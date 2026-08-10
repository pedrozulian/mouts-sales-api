# Implementation Plan: Consultar Venda

**Branch**: `003-consultar-venda` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-consultar-venda/spec.md`

## Summary

Consultar venda (`GET /api/sales/{id}`): retorna a representação completa de uma venda já
registrada — cliente, filial, itens ativos e cancelados, descontos e totais já calculados —
sem recalcular nada e sem disparar eventos de domínio. Abordagem: query MediatR
(`GetSaleQuery`) na camada Application, lendo o agregado `Sale` existente via
`IApplicationDbContext` com `AsNoTracking` + `Include(Items)`, reaproveitando os DTOs
(`SaleResponse`/`SaleItemResponse`) e o mapeamento Mapster já registrados pela feature 002;
endpoint Minimal API na camada Api traduzindo `Result` para `200`/`404`. Nenhuma alteração é
necessária no Domain nem na Infrastructure.

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS)

**Primary Dependencies**: ASP.NET Core Minimal APIs (endpoint adicional em
`SalesApi.Api.Sales.SalesEndpoints`), MediatR 14.2.0 (query e handler), Mapster 10.0.11
(reaproveitando o mapeamento `Sale → SaleResponse` já registrado por
`CreateSaleMappingConfig`), Entity Framework Core 8 via
`Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11 (leitura via `IApplicationDbContext.Sales`),
Serilog.AspNetCore (log estruturado no handler)

**Storage**: PostgreSQL 16, leitura somente (`AsNoTracking`) das tabelas `sales` e
`sale_items` já criadas pela migration `CreateSales` (002-registrar-venda); nenhuma nova
tabela ou migration

**Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`)
para o teste de ponta a ponta do endpoint; `coverlet.collector` para cobertura; testes que
exigem estado "cancelado" preparam esse estado diretamente no banco (ver `research.md`, seção
6), já que UC-05/UC-06 ainda não existem

**Target Platform**: serviço web ASP.NET Core containerizado (Linux, Docker), execução local
via `docker compose` ou `dotnet run` — mesmo ambiente já orquestrado pela feature 002

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já
existentes: Domain, Application, Infrastructure, Api); esta feature adiciona código apenas em
Application e Api

**Performance Goals**: sem meta formal de throughput — protótipo de avaliação técnica com uso
local/single-instance; resposta síncrona

**Constraints**: cobertura mínima de 90% (Princípio IX); consulta não pode recalcular
desconto/total nem revalidar regras de registro (FR-010); nenhum evento de domínio disparado
(FR-011); nenhuma alteração de estado (FR-009)

**Scale/Scope**: um único endpoint (`GET /api/sales/{id}`); volume de dados de teste, sem
carga concorrente real

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | Nenhuma lógica de negócio nova; a consulta apenas expõe o agregado `Sale` já validado e persistido pela feature 002. Nenhum recálculo de desconto/total (FR-010). | PASS |
| II. TDD | Tasks (a gerar via `/speckit-tasks`) seguem Red-Green-Refactor: teste de `GetSaleQueryHandler` e do endpoint escritos antes da implementação. | PASS |
| III. SOLID | `GetSaleQueryHandler` com responsabilidade única (buscar e mapear); `IApplicationDbContext` injetado por interface via construtor. | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md` e o contrato estão em português; identificadores de código permanecem em inglês. | PASS |
| V. Clean Architecture | Código novo restrito a Application (`Sales/Get/`) e Api (extensão de `SalesEndpoints`); Domain e Infrastructure não são alterados. | PASS |
| VI. Eventos via Mediator | Não se aplica a eventos: consulta não dispara nenhum evento de domínio (FR-011). MediatR é usado apenas como Query/Handler. | PASS (N/A eventos) |
| VII. Result/Notification | "Venda não encontrada" é comunicado via `Result<SaleResponse>.Failure(Notification)`, nunca exception; endpoint traduz para `404`. | PASS |
| VIII. Observabilidade | Logging estruturado explícito no `GetSaleQueryHandler` (encontrada/não encontrada), já que não há evento de domínio para carregar o log. | PASS |
| IX. Qualidade de Código | Tasks incluem testes suficientes para manter o gate de 90% de cobertura verificado no CI (SonarCloud). | PASS |
| X. Docker | Nenhum novo recurso de infraestrutura — reaproveita PostgreSQL já orquestrado pelo `docker-compose.yml` existente. | PASS |

Nenhuma violação identificada. `Complexity Tracking` não se aplica.

**Reavaliação pós Fase 1**: `data-model.md` não introduziu nenhuma entidade nova (apenas
projeção de leitura do agregado existente); `contracts/get-sale.md` não introduziu nenhum
campo fora do já modelado por `002-registrar-venda`. Gate permanece PASS.

## Project Structure

### Documentation (this feature)

```text
specs/003-consultar-venda/
├── plan.md                  # Este arquivo (/speckit-plan)
├── research.md               # Fase 0 (/speckit-plan)
├── data-model.md              # Fase 1 (/speckit-plan)
├── quickstart.md              # Fase 1 (/speckit-plan)
├── contracts/
│   └── get-sale.md            # Fase 1 (/speckit-plan)
├── checklists/
│   └── requirements.md
└── tasks.md                   # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/                     # SEM ALTERAÇÃO — reaproveita Sale/SaleItem/ExternalReference já existentes
│
├── SalesApi.Application/
│   └── Sales/
│       ├── Create/                      # já existe (002-registrar-venda)
│       ├── Dtos/                        # já existe — SaleResponse, SaleItemResponse, ExternalReferenceResponse reaproveitados
│       └── Get/                         # NOVO
│           ├── GetSaleQuery.cs          # IRequest<Result<SaleResponse>> — parâmetro: Id (Guid)
│           └── GetSaleQueryHandler.cs   # IApplicationDbContext.Sales.AsNoTracking().Include(Items), log estruturado
│
├── SalesApi.Infrastructure/             # SEM ALTERAÇÃO — reaproveita AppDbContext.Sales e a migration existente
│
└── SalesApi.Api/
    └── Sales/
        └── SalesEndpoints.cs            # ajustar: adicionar MapGet("/api/sales/{id:guid}", GetSale)

tests/
├── SalesApi.Application.Tests/Sales/
│   └── GetSaleQueryHandlerTests.cs      # venda encontrada, não encontrada, com item cancelado, cancelada integralmente
└── SalesApi.Api.Tests/Sales/
    └── GetSaleEndpointTests.cs          # WebApplicationFactory, 200 e 404, Postgres local (mesmo padrão de AppDbContextConnectionTests)
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida (4
projetos), sem criar nenhum projeto novo. Diferente de `002-registrar-venda`, esta feature não
toca em `SalesApi.Domain` nem em `SalesApi.Infrastructure` — é uma leitura pura sobre o
agregado e a tabela já existentes. O código novo se concentra em uma pasta `Get/` dentro do
mesmo agrupamento `Sales/` da Application (mesmo padrão de organização por bounded context da
feature anterior) e na extensão do `MapSalesEndpoints` já existente na Api, evitando um
segundo arquivo de endpoints para o mesmo recurso.

## Complexity Tracking

*Sem violações — seção não se aplica a este plano.*
