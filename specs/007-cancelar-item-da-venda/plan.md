# Implementation Plan: Cancelar Item da Venda

**Branch**: `007-cancelar-item-da-venda` | **Date**: 2026-08-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-cancelar-item-da-venda/spec.md`

## Summary

Cancelar item da venda (`DELETE /api/sales/{id}/items/{itemId}`): cancela logicamente um item
ativo de uma venda ativa, recalcula o total geral somando apenas os itens ainda ativos e responde
`204` sem corpo (FR-001 a FR-010). Quando o item cancelado é o último ainda ativo, a venda inteira
também é cancelada na mesma operação (FR-009/FR-012, INV-09). Abordagem: um novo método de
escrita no agregado `Sale` (`Sale.CancelItem(Guid itemId)`) que, ao detectar que nenhum item
permanece ativo após o cancelamento, **reaproveita `Sale.Cancel()`** (já existente desde a feature
006) para aplicar a cascata — evitando duplicar a lógica de "encerrar a venda" em dois lugares.
Um novo Command MediatR (`CancelSaleItemCommand`) carrega a venda com tracking (mesmo padrão de
`CancelSaleCommandHandler`/`UpdateSaleCommandHandler`), delega ao agregado e persiste. A
concorrência (FR-015 — duas requisições concorrentes para o mesmo item) é satisfeita **sem
nenhuma mudança de Infrastructure**: o token `xmin` já mapeado em `SaleConfiguration` (006) cobre
qualquer escrita na linha de `Sale`, e `CancelItem` sempre grava `TotalAmount`/`UpdatedAt` nessa
mesma linha — a segunda requisição a chegar encontra `xmin` divergente e recebe
`DbUpdateConcurrencyException`, traduzida para `400`. Dois eventos de domínio já existentes
(`ItemCancelled`, `SaleCancelled`) são emitidos — um ou ambos, conforme a cascata se aplique ou
não — sem introduzir nenhum evento novo.

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS)

**Primary Dependencies**: MediatR 14.2.0 (novo `CancelSaleItemCommand` + handler, retornando
`Result` não genérico — não há corpo de resposta em `204`; nenhum `INotificationHandler` novo,
pois `ItemCancelledEventHandler` e `SaleCancelledEventHandler` já existem desde as features 005 e
006), Entity Framework Core 8 via `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11 (leitura **com
tracking** via `IApplicationDbContext.Sales.Include(s => s.Items)`, reaproveitando o token de
concorrência `xmin` já mapeado por `SaleConfiguration` — nenhuma configuração nova), ASP.NET Core
Minimal APIs (novo `MapDelete("/api/sales/{id:guid}/items/{itemId:guid}", ...)` em
`SalesEndpoints`), Serilog.AspNetCore (log estruturado no novo handler). Nenhuma dependência nova
é adicionada aos `.csproj` existentes.

**Storage**: PostgreSQL 16. Reaproveita as tabelas `sales`/`sale_items` já criadas pela migration
`CreateSales` (`002-registrar-venda`) — `is_cancelled` (em ambas) e `total_amount` (em ambas) já
cobrem o que o cancelamento de item precisa gravar. Nenhuma migration nova.

**Testing**: xUnit. `SalesApi.Domain.Tests/Sales/SaleTests.cs` recebe os novos casos de
`Sale.CancelItem()` (item ativo entre outros ativos, item já cancelado individualmente, venda já
cancelada, item inexistente, cascata ao cancelar o último item ativo, emissão de `ItemCancelled` e
de `SaleCancelled` quando a cascata se aplica). `SalesApi.Application.Tests/Sales/` recebe
`CancelSaleItemCommandHandlerTests.cs` (fluxo principal, venda não encontrada, item não encontrado,
venda já cancelada, item já cancelado, conflito de concorrência traduzido para `400`).
`SalesApi.Api.Tests/Sales/` recebe `CancelSaleItemEndpointTests.cs` via
`WebApplicationFactory<Program>` (`204`/`400`/`404` ponta a ponta, Postgres local) e
`CancelSaleItemConcurrencyTests.cs` — duas requisições `DELETE` concorrentes para o mesmo item via
`Task.WhenAll`, mesmo padrão de `CancelSaleConcurrencyTests.cs` (006) — verificando exatamente uma
`204` e uma `400` (FR-015).

**Target Platform**: serviço web ASP.NET Core containerizado (Linux, Docker), execução local via
`docker compose` ou `dotnet run` — mesmo ambiente já orquestrado pela feature 002.

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já existentes:
Domain, Application, Infrastructure, Api). Esta é a terceira feature, depois da alteração (005) e
do cancelamento de venda (006), a acrescentar um método de escrita ao agregado `Sale`. Diferente
de 006, esta feature **não toca Infrastructure** — o token de concorrência já existente é
suficiente.

**Performance Goals**: sem meta formal de throughput — protótipo de avaliação técnica com uso
local/single-instance. O cancelamento opera sobre um único item de uma única venda por requisição.

**Constraints**: cobertura mínima de 90% (Princípio IX); venda cancelada é estado imutável — não
aceita cancelamento de item (INV-07/FR-005); item já cancelado não pode ser cancelado de novo
(INV-08/FR-008); cancelar o último item ativo cancela a venda (INV-09/FR-009); cancelamento é
sempre lógico, nunca remoção física (FR-004); resposta de sucesso não tem corpo (FR-010); toda
resposta de erro segue o mesmo contrato `{ "errors": [...] }` já usado pelos demais casos de uso;
entre duas solicitações de cancelamento concorrentes para o mesmo item, exatamente uma é aplicada
— a outra recebe `400` como se o item já estivesse cancelado, nunca um erro não tratado (FR-015).

**Scale/Scope**: um único endpoint (`DELETE /api/sales/{id}/items/{itemId}`); volume de dados de
teste, sem carga real além do necessário para validar FR-015 concorrentemente.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | Toda a lógica de cancelamento de item (verificar imutabilidade da venda, localizar o item, verificar se já está cancelado, recalcular o total, decidir a cascata) vive em `Sale.CancelItem()`, nunca no handler ou no endpoint. | PASS |
| II. TDD | Tasks (a gerar via `/speckit-tasks`) seguem Red-Green-Refactor: testes de `Sale.CancelItem()` para cada cenário e cada invariante são escritos antes da implementação do método. | PASS |
| III. SOLID | `CancelSaleItemCommandHandler` com responsabilidade única (carregar a venda, delegar ao domínio, persistir, traduzir conflito de concorrência); `IApplicationDbContext` injetado por interface via construtor; a cascata para `Sale.Cancel()` é reaproveitada em vez de duplicada, evitando duas implementações divergentes da mesma regra (INV-09) — reforça SRP/DRY. | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md` e o contrato estão em português; identificadores de código permanecem em inglês. | PASS |
| V. Clean Architecture | Código novo/ajustado em Domain (`Sale.CancelItem()`), Application (`Sales/CancelItem/`, sem novo event handler — reaproveita os já existentes) e Api (extensão de `SalesEndpoints`). Infrastructure **não é tocada** — diferença notável em relação à feature 006. | PASS |
| VI. Eventos via Mediator | `ItemCancelled` (já existente, introduzido por 005) é sempre emitido por cancelamento de item bem-sucedido; `SaleCancelled` (já existente, introduzido por 006) é emitido adicionalmente apenas quando a cascata se aplica (FR-011/FR-012) — ambos acumulados no agregado via `AddDomainEvent` e despachados pelo `AppDbContext.SaveChangesAsync` já genérico. | PASS |
| VII. Result/Notification | Toda violação (venda não encontrada, item não encontrado, venda já cancelada, item já cancelado, conflito de concorrência) é comunicada via `Result.Failure(Notification[])`, nunca exception vazando para o chamador — inclusive `DbUpdateConcurrencyException`, capturada e traduzida no handler (ver `research.md`, seção 4). | PASS |
| VIII. Observabilidade | Logging estruturado explícito no novo `CancelSaleItemCommandHandler` (venda não encontrada, item não encontrado, conflito de concorrência, sucesso), no mesmo formato de `CancelSaleCommandHandler`. | PASS |
| IX. Qualidade de Código | Tasks incluem testes para cada invariante, os três caminhos de resposta (`204`/`400`/`404`) e o cenário de concorrência (FR-015), necessários para manter o gate de 90% de cobertura verificado no CI (SonarCloud). | PASS |
| X. Docker | Nenhum novo recurso de infraestrutura — a feature roda inteiramente sobre o PostgreSQL já orquestrado pelo `docker-compose.yml` existente, sem migration nova e sem ajuste algum em `Infrastructure`. | PASS |

Nenhuma violação identificada. `Complexity Tracking` está vazio — esta feature não introduz
nenhuma exceção aos princípios, ao contrário de 006 (que precisou justificar o ajuste em
`UpdateSaleCommandHandler`).

**Reavaliação pós Fase 1**: `data-model.md` acrescenta um método de agregado
(`Sale.CancelItem()`), sem nenhum evento de domínio novo — `ItemCancelled` e `SaleCancelled` já
estavam previstos na documentação DDG do Notion e implementados pelas features 005 e 006. A
reutilização do token `xmin` (Infrastructure, decisão de 006) é inteiramente transparente ao
Domain e à Application — nenhuma mudança de schema, nenhuma mudança de configuração EF Core.
`contracts/cancel-sale-item.md` não introduz nenhum campo de resposta novo — a resposta de sucesso
é `204` sem corpo, como já previsto no Notion (UC-06, tabela "Superfície da API"). Gate permanece
PASS.

## Project Structure

### Documentation (this feature)

```text
specs/007-cancelar-item-da-venda/
├── plan.md                    # Este arquivo (/speckit-plan)
├── research.md                # Fase 0 (/speckit-plan)
├── data-model.md              # Fase 1 (/speckit-plan)
├── quickstart.md              # Fase 1 (/speckit-plan)
├── contracts/
│   └── cancel-sale-item.md    # Fase 1 (/speckit-plan)
├── checklists/
│   └── requirements.md
└── tasks.md                   # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/
│   └── Sales/
│       ├── Sale.cs                        # AJUSTAR — novo método público CancelItem(Guid itemId)
│       └── Events/
│           ├── SaleCreated.cs             # já existe
│           ├── SaleModified.cs            # já existe
│           ├── ItemCancelled.cs           # já existe — reaproveitado sem alteração
│           └── SaleCancelled.cs           # já existe — reaproveitado sem alteração
│
├── SalesApi.Application/
│   └── Sales/
│       ├── Create/                        # já existe
│       ├── Get/                           # já existe
│       ├── List/                          # já existe
│       ├── Update/                        # já existe
│       ├── Cancel/                        # já existe (006)
│       ├── Events/
│       │   ├── SaleCreatedEventHandler.cs     # já existe
│       │   ├── SaleModifiedEventHandler.cs    # já existe
│       │   ├── ItemCancelledEventHandler.cs   # já existe — reaproveitado sem alteração
│       │   └── SaleCancelledEventHandler.cs   # já existe — reaproveitado sem alteração
│       └── CancelItem/                    # NOVO
│           ├── CancelSaleItemCommand.cs       # IRequest<Result>; SaleId e ItemId vêm da rota
│           └── CancelSaleItemCommandHandler.cs # carrega com tracking, delega a Sale.CancelItem(), salva, captura conflito de concorrência
│
├── SalesApi.Infrastructure/               # NÃO TOCADO — xmin já mapeado em SaleConfiguration (006) cobre esta feature
│
└── SalesApi.Api/
    └── Sales/
        └── SalesEndpoints.cs              # AJUSTAR — adicionar MapDelete("/api/sales/{id:guid}/items/{itemId:guid}", CancelSaleItem)

tests/
├── SalesApi.Domain.Tests/Sales/
│   └── SaleTests.cs                       # AJUSTAR — casos de CancelItem(): item ativo entre outros, item já cancelado individualmente permanecendo inalterado, venda já cancelada, item inexistente, item já cancelado, cascata no último item ativo, emissão de ItemCancelled/SaleCancelled
├── SalesApi.Application.Tests/Sales/
│   └── CancelSaleItemCommandHandlerTests.cs   # NOVO — fluxo principal, venda não encontrada, item não encontrado, venda já cancelada, item já cancelado, conflito de concorrência
└── SalesApi.Api.Tests/Sales/
    ├── CancelSaleItemEndpointTests.cs         # NOVO — WebApplicationFactory, 204/400/404, Postgres local
    └── CancelSaleItemConcurrencyTests.cs      # NOVO — duas requisições DELETE concorrentes para o mesmo item, mesmo padrão de CancelSaleConcurrencyTests.cs (006)
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida (4 projetos),
sem criar nenhum projeto novo. Diferente de `006-cancelar-venda` — que precisou ajustar
`Infrastructure` (token `xmin`) e, por efeito colateral, `UpdateSaleCommandHandler` — esta feature
toca apenas Domain, Application e Api: o token de concorrência introduzido em 006 já cobre
qualquer escrita na linha de `Sale`, incluindo as originadas por `CancelItem`. O código novo de
Application segue o mesmo padrão de pastas por caso de uso dentro de `Sales/` (`CancelItem/`, ao
lado de `Create/`, `Get/`, `List/`, `Update/` e `Cancel/`). Nenhum event handler novo é necessário
— `ItemCancelledEventHandler` (005) e `SaleCancelledEventHandler` (006) já cobrem os dois eventos
que esta feature pode emitir.

## Complexity Tracking

*Nenhuma violação da constitution identificada — tabela vazia.*
