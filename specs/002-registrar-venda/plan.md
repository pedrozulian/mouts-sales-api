# Implementation Plan: Registrar Venda

**Branch**: `002-registrar-venda` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-registrar-venda/spec.md`

## Summary

Registrar venda (`POST /api/sales`): recebe cliente, filial e itens (produto, quantidade,
preço unitário) via External Identities, calcula o desconto progressivo por faixa de
quantidade dentro do agregado `Sale` do bounded context Sales, persiste em uma única
transação no PostgreSQL via EF Core, e publica o evento de domínio `SaleCreated` (log
estruturado) através do MediatR após o commit. Abordagem: comando MediatR
(`CreateSaleCommand`) na camada Application orquestrando o caso de uso; agregado
`Sale`/`SaleItem` com a política de desconto e as invariantes na camada Domain; endpoint
Minimal API na camada Api traduzindo `Result` para HTTP.

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS)

**Primary Dependencies**: ASP.NET Core Minimal APIs (endpoint em `SalesApi.Api`), MediatR
14.2.0 (comandos e eventos de domínio), Mapster 10.0.11 (mapeamento DTO ↔ domínio), Entity
Framework Core 8 via `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11, Serilog.AspNetCore (log
estruturado), Swashbuckle.AspNetCore (Swagger/OpenAPI)

**Storage**: PostgreSQL 16, via `AppDbContext` (EF Core) — novas tabelas `sales` e
`sale_items`

**Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`)
para testes de integração, `coverlet.collector` para cobertura; testes de integração exigem
PostgreSQL local ativo (mesmo padrão já usado por `AppDbContextConnectionTests`)

**Target Platform**: serviço web ASP.NET Core containerizado (Linux, Docker), execução local
via `docker compose` ou `dotnet run`

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já
existentes: Domain, Application, Infrastructure, Api)

**Performance Goals**: sem meta formal de throughput — protótipo de avaliação técnica com uso
local/single-instance; resposta síncrona, sem filas ou processamento assíncrono

**Constraints**: cobertura mínima de 90% (Princípio IX); nenhuma regra de negócio fora da
camada Domain (Princípios I e V); nenhuma chamada de rede a sistemas externos para validar
cliente/filial/produto (Assumption da spec); evento `SaleCreated` publicado apenas via log
estruturado, sem broker real (Assumption da spec)

**Scale/Scope**: um único endpoint (`POST /api/sales`); volume de dados de teste, sem carga
concorrente real

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | Regras de desconto e invariantes (INV-01 a INV-05, INV-10) residem inteiramente no agregado `Sale`/`SaleItem` (Domain). Nenhuma lógica de negócio no endpoint ou no handler. | PASS |
| II. TDD | Tasks (a gerar via `/speckit-tasks`) seguem Red-Green-Refactor: testes de `DiscountPolicy`, `Sale.Create` e do handler escritos antes da implementação. | PASS |
| III. SOLID | `CreateSaleCommandHandler` com responsabilidade única (orquestrar o caso de uso); dependências (`IApplicationDbContext`, `IPublisher`) injetadas por interface via construtor. | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md` e o contrato estão em português; identificadores de código permanecem em inglês. | PASS |
| V. Clean Architecture | Novo código distribuído estritamente entre as 4 camadas existentes; nenhuma referência de projeto na direção errada. | PASS |
| VI. Eventos via Mediator | `SaleCreated` implementa `DomainEvent` (`INotification`); despachado via `IPublisher` do MediatR após `SaveChangesAsync` bem-sucedido. | PASS |
| VII. Result/Notification | `Sale.Create` e o handler retornam `Result`/`Result<T>`; nenhuma exception usada para regra de negócio violada. | PASS |
| VIII. Observabilidade | Log estruturado no handler (início/fim do comando) e no handler do evento `SaleCreated`, reaproveitando o `CorrelationId` já propagado pelo middleware existente. | PASS |
| IX. Qualidade de Código | Tasks incluem testes suficientes para manter o gate de 90% de cobertura verificado no CI (SonarCloud). | PASS |
| X. Docker | Nenhum novo recurso de infraestrutura — reaproveita o PostgreSQL já orquestrado pelo `docker-compose.yml` existente. | PASS |

Nenhuma violação identificada. `Complexity Tracking` não se aplica.

**Reavaliação pós Fase 1**: `data-model.md` e `contracts/create-sale.md` não introduziram
nenhum elemento fora das 4 camadas nem nova dependência de stack. Gate permanece PASS.

## Project Structure

### Documentation (this feature)

```text
specs/002-registrar-venda/
├── plan.md                  # Este arquivo (/speckit-plan)
├── research.md               # Fase 0 (/speckit-plan)
├── data-model.md              # Fase 1 (/speckit-plan)
├── quickstart.md              # Fase 1 (/speckit-plan)
├── contracts/
│   └── create-sale.md         # Fase 1 (/speckit-plan)
├── checklists/
│   └── requirements.md
└── tasks.md                   # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/
│   ├── Common/                          # ajustar: Entity ganha suporte a domain events (DomainEvent, Result/Notification já existem)
│   └── Sales/                           # NOVO — bounded context Sales
│       ├── Sale.cs                      # aggregate root
│       ├── SaleItem.cs                  # entidade filha
│       ├── ExternalReference.cs         # value object (Id + Name)
│       ├── DiscountPolicy.cs            # faixas de desconto por quantidade
│       └── Events/
│           └── SaleCreated.cs           # evento de domínio
│
├── SalesApi.Application/
│   ├── Common/                          # já existe: IApplicationDbContext
│   └── Sales/
│       ├── Create/
│       │   ├── CreateSaleCommand.cs
│       │   ├── CreateSaleCommandHandler.cs
│       │   └── CreateSaleMappingConfig.cs   # Mapster: request/domínio → response
│       ├── Dtos/
│       │   ├── CreateSaleRequest.cs
│       │   ├── ExternalReferenceRequest.cs / ExternalReferenceResponse.cs
│       │   ├── SaleItemRequest.cs / SaleItemResponse.cs
│       │   └── SaleResponse.cs
│       └── Events/
│           └── SaleCreatedEventHandler.cs   # log estruturado (Princípio VIII)
│
├── SalesApi.Infrastructure/
│   └── Persistence/
│       ├── AppDbContext.cs              # ajustar: DbSet<Sale>, despacho de domain events no SaveChanges
│       ├── Configurations/
│       │   ├── SaleConfiguration.cs     # mapeamento EF Core (owned types para ExternalReference)
│       │   └── SaleItemConfiguration.cs
│       └── Migrations/                  # gerada via `dotnet ef migrations add`
│
└── SalesApi.Api/
    ├── Program.cs                       # ajustar: registrar app.MapSalesEndpoints()
    └── Sales/
        └── SalesEndpoints.cs            # POST /api/sales + tradução Result → HTTP

tests/
├── SalesApi.Domain.Tests/Sales/
│   ├── DiscountPolicyTests.cs
│   └── SaleTests.cs
├── SalesApi.Application.Tests/Sales/
│   └── CreateSaleCommandHandlerTests.cs
└── SalesApi.Api.Tests/Sales/
    └── CreateSaleEndpointTests.cs       # WebApplicationFactory, Postgres local (mesmo padrão de AppDbContextConnectionTests)
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida (4
projetos). Nenhum projeto novo é criado; a feature adiciona uma pasta `Sales/` por camada,
seguindo o mesmo agrupamento por bounded context descrito na documentação DDD do Notion.
Endpoints usam Minimal API (extension method `MapSalesEndpoints`), consistente com o único
endpoint hoje existente (`/health`), mapeado diretamente em `Program.cs`.

## Complexity Tracking

*Sem violações — seção não se aplica a este plano.*
