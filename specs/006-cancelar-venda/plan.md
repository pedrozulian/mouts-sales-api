# Implementation Plan: Cancelar Venda

**Branch**: `006-cancelar-venda` | **Date**: 2026-08-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-cancelar-venda/spec.md`

## Summary

Cancelar venda (`DELETE /api/sales/{id}`): cancela logicamente uma venda ativa e todos os seus
itens ainda ativos, zera o total geral e responde `204` sem corpo (FR-001 a FR-007). Abordagem:
um novo método de escrita no agregado `Sale` (`Sale.Cancel()`), mais simples que `Sale.Update`
por não receber nenhum dado externo para validar — a única regra é a imutabilidade de venda já
cancelada (INV-07/FR-005). Um novo Command MediatR (`CancelSaleCommand`) carrega a venda **com
tracking** (`Include(Items)`, mesmo padrão de `UpdateSaleCommandHandler`), delega ao agregado e
persiste. A decisão técnica central desta feature é como satisfazer FR-013 (duas solicitações de
cancelamento concorrentes para a mesma venda — decisão da sessão de `/speckit-clarify`: a que
perder a corrida recebe `400`, nunca duplo cancelamento nem dois eventos): o projeto ainda não
tem nenhum mecanismo de concorrência otimista, então esta feature introduz o `xmin` do
PostgreSQL como token de concorrência do EF Core para `Sale` — nativo do banco, sem exigir
nenhuma migration. Um único evento de domínio `SaleCancelled` é emitido por cancelamento
bem-sucedido, nunca um `ItemCancelled` por item (FR-008/FR-009), reforçando a leitura do Notion
de que o cancelamento em massa é um único fato de negócio.

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS)

**Primary Dependencies**: MediatR 14.2.0 (novo `CancelSaleCommand` + handler, retornando
`Result` não genérico — não há corpo de resposta em `204`; novo `INotificationHandler<SaleCancelled>`),
Entity Framework Core 8 via `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11 (leitura **com
tracking** via `IApplicationDbContext.Sales.Include(s => s.Items)`, mais
`builder.Property<uint>("xmin").IsRowVersion()` — forma padrão do EF Core para mapear o `xmin`
do PostgreSQL como token de concorrência sem coluna nova; o método de conveniência
`UseXminAsConcurrencyToken()` do provider Npgsql está obsoleto na versão em uso), ASP.NET Core
Minimal APIs (novo
`MapDelete("/api/sales/{id:guid}", ...)` em `SalesEndpoints`), Serilog.AspNetCore (log
estruturado no novo handler e no novo event handler). Nenhuma dependência nova é adicionada aos
`.csproj` existentes.

**Storage**: PostgreSQL 16. Reaproveita as tabelas `sales`/`sale_items` já criadas pela migration
`CreateSales` (`002-registrar-venda`) — `is_cancelled` e `total_amount` de ambas as tabelas já
cobrem o que o cancelamento precisa gravar. O `xmin` é uma coluna de sistema do PostgreSQL,
sempre presente em toda tabela — mapeá-la como token de concorrência não requer nenhuma migration
nova, apenas configuração no `OnModelCreating`.

**Testing**: xUnit. `SalesApi.Domain.Tests/Sales/SaleTests.cs` recebe os novos casos de
`Sale.Cancel()` (venda com itens ativos e já parcialmente cancelados, INV-06, INV-07, emissão de
`SaleCancelled`). `SalesApi.Application.Tests/Sales/` recebe `CancelSaleCommandHandlerTests`
(fluxo principal, venda não encontrada, venda já cancelada, conflito de concorrência traduzido
para `400`) e um teste para o novo event handler. `SalesApi.Api.Tests/Sales/` recebe
`CancelSaleEndpointTests` via `WebApplicationFactory<Program>` (`204`/`400`/`404` ponta a ponta,
Postgres local) e `CancelSaleConcurrencyTests` — duas requisições `DELETE` concorrentes para a
mesma venda via `Task.WhenAll`, mesmo padrão de `CreateSaleConcurrencyTests.cs` — verificando
exatamente uma `204` e uma `400` (FR-013).

**Target Platform**: serviço web ASP.NET Core containerizado (Linux, Docker), execução local via
`docker compose` ou `dotnet run` — mesmo ambiente já orquestrado pela feature 002.

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já existentes:
Domain, Application, Infrastructure, Api). Esta é a segunda feature, depois da alteração (005), a
acrescentar um método de escrita ao agregado `Sale`: toca Domain, Application, Infrastructure
(configuração de concorrência) e Api.

**Performance Goals**: sem meta formal de throughput — protótipo de avaliação técnica com uso
local/single-instance. O cancelamento opera sobre uma única venda por requisição.

**Constraints**: cobertura mínima de 90% (Princípio IX); venda cancelada é estado imutável — um
segundo cancelamento é rejeitado com `400` (INV-07/FR-005); cancelamento é sempre lógico, nunca
remoção física (FR-004); resposta de sucesso não tem corpo (FR-007); toda resposta de erro segue
o mesmo contrato `{ "errors": [...] }` já usado pelos demais casos de uso; entre duas
solicitações de cancelamento concorrentes para a mesma venda, exatamente uma é aplicada — a
outra recebe `400` como se a venda já estivesse cancelada, nunca um erro não tratado (FR-013,
decisão da sessão de clarificação).

**Scale/Scope**: um único endpoint (`DELETE /api/sales/{id}`); volume de dados de teste, sem
carga real além do necessário para validar FR-013 concorrentemente.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | Toda a lógica de cancelamento (verificar imutabilidade, cancelar itens ativos, zerar total) vive em `Sale.Cancel()`, nunca no handler ou no endpoint. | PASS |
| II. TDD | Tasks (a gerar via `/speckit-tasks`) seguem Red-Green-Refactor: testes de `Sale.Cancel()` para cada cenário e cada invariante são escritos antes da implementação do método. | PASS |
| III. SOLID | `CancelSaleCommandHandler` com responsabilidade única (carregar a venda, delegar ao domínio, persistir, traduzir conflito de concorrência); `IApplicationDbContext` injetado por interface via construtor; a regra de cancelamento fica centralizada em `Sale.Cancel()` em vez de duplicada entre handler e endpoint (SRP). | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md` e o contrato estão em português; identificadores de código permanecem em inglês. | PASS |
| V. Clean Architecture | Código novo/ajustado em Domain (`Sale.Cancel()`, evento `SaleCancelled`), Application (`Sales/Cancel/`, novo event handler, ajuste pontual em `UpdateSaleCommandHandler`), Infrastructure (`SaleConfiguration` — token de concorrência) e Api (extensão de `SalesEndpoints`). | PASS |
| VI. Eventos via Mediator | `SaleCancelled` (um único por cancelamento bem-sucedido, nunca um `ItemCancelled` por item — FR-008/FR-009) é acumulado no agregado via `AddDomainEvent` e despachado pelo `AppDbContext.SaveChangesAsync` já genérico — nenhuma alteração de infraestrutura de despacho necessária. | PASS |
| VII. Result/Notification | Toda violação (venda não encontrada, venda já cancelada, conflito de concorrência) é comunicada via `Result.Failure(Notification[])`, nunca exception vazando para o chamador — inclusive `DbUpdateConcurrencyException`, capturada e traduzida no handler (ver `research.md`, seção 3). | PASS |
| VIII. Observabilidade | Logging estruturado explícito no `CancelSaleCommandHandler` (venda não encontrada, conflito de concorrência, sucesso) e no novo `INotificationHandler<SaleCancelled>`, no mesmo formato de `SaleModifiedEventHandler`. | PASS |
| IX. Qualidade de Código | Tasks incluem testes para cada invariante, os três caminhos de resposta (`204`/`400`/`404`) e o cenário de concorrência (FR-013), necessários para manter o gate de 90% de cobertura verificado no CI (SonarCloud). | PASS |
| X. Docker | Nenhum novo recurso de infraestrutura — a feature roda inteiramente sobre o PostgreSQL já orquestrado pelo `docker-compose.yml` existente, sem migration nova (`xmin` é coluna de sistema, não requer DDL). | PASS |

Nenhuma violação identificada. `Complexity Tracking` registra apenas o ajuste pontual e
justificado em `UpdateSaleCommandHandler` (005), não uma violação de princípio.

**Reavaliação pós Fase 1**: `data-model.md` acrescenta um método de agregado (`Sale.Cancel()`) e
um evento de domínio (`SaleCancelled`) — ambos já previstos na documentação DDD do Notion
(diagrama de classes e seção "Eventos de domínio"). O token de concorrência `xmin` é uma decisão
de persistência pura (Infrastructure), sem introduzir nenhum conceito novo no Domain nem no
contrato de resposta. `contracts/cancel-sale.md` não introduz nenhum campo de resposta — a
resposta de sucesso é `204` sem corpo, como já previsto no Notion (UC-05, tabela "Superfície da
API"). Gate permanece PASS.

## Project Structure

### Documentation (this feature)

```text
specs/006-cancelar-venda/
├── plan.md                    # Este arquivo (/speckit-plan)
├── research.md                # Fase 0 (/speckit-plan)
├── data-model.md               # Fase 1 (/speckit-plan)
├── quickstart.md               # Fase 1 (/speckit-plan)
├── contracts/
│   └── cancel-sale.md         # Fase 1 (/speckit-plan)
├── checklists/
│   └── requirements.md
└── tasks.md                   # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/
│   └── Sales/
│       ├── Sale.cs                        # AJUSTAR — novo método público Cancel()
│       └── Events/
│           ├── SaleCreated.cs             # já existe
│           ├── SaleModified.cs            # já existe
│           ├── ItemCancelled.cs           # já existe
│           └── SaleCancelled.cs           # NOVO — SaleId, SaleNumber, OccurredOn
│
├── SalesApi.Application/
│   └── Sales/
│       ├── Create/                        # já existe
│       ├── Get/                           # já existe
│       ├── List/                          # já existe
│       ├── Update/
│       │   └── UpdateSaleCommandHandler.cs    # AJUSTAR — captura DbUpdateConcurrencyException e traduz para Result.Failure("sale", ...), mesma mensagem já usada para venda cancelada (consequência de introduzir o token de concorrência nesta feature; ver research.md, seção 3)
│       ├── Events/
│       │   ├── SaleCreatedEventHandler.cs     # já existe
│       │   ├── SaleModifiedEventHandler.cs    # já existe
│       │   ├── ItemCancelledEventHandler.cs   # já existe
│       │   └── SaleCancelledEventHandler.cs   # NOVO — log estruturado
│       └── Cancel/                        # NOVO
│           ├── CancelSaleCommand.cs           # IRequest<Result>; Id vem da rota
│           └── CancelSaleCommandHandler.cs    # carrega com tracking, delega a Sale.Cancel(), salva, captura conflito de concorrência
│
├── SalesApi.Infrastructure/
│   └── Persistence/
│       └── Configurations/
│           └── SaleConfiguration.cs       # AJUSTAR — builder.Property<uint>("xmin").IsRowVersion()
│
└── SalesApi.Api/
    └── Sales/
        └── SalesEndpoints.cs              # AJUSTAR — adicionar MapDelete("/api/sales/{id:guid}", CancelSale)

tests/
├── SalesApi.Domain.Tests/Sales/
│   └── SaleTests.cs                       # AJUSTAR — casos de Cancel(): venda com itens ativos, venda já cancelada, itens já cancelados individualmente, emissão de SaleCancelled
├── SalesApi.Application.Tests/Sales/
│   ├── Update/UpdateSaleCommandHandlerTests.cs # AJUSTAR — novo caso: conflito de concorrência traduzido para 400
│   ├── CancelSaleCommandHandlerTests.cs   # NOVO — fluxo principal, venda não encontrada, venda já cancelada, conflito de concorrência
│   └── Events/
│       └── SaleCancelledEventHandlerTests.cs  # NOVO
└── SalesApi.Api.Tests/Sales/
    ├── CancelSaleEndpointTests.cs         # NOVO — WebApplicationFactory, 204/400/404, Postgres local
    └── CancelSaleConcurrencyTests.cs      # NOVO — duas requisições DELETE concorrentes, mesmo padrão de CreateSaleConcurrencyTests.cs
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida (4
projetos), sem criar nenhum projeto novo. Diferente de `005-alterar-venda` — que não tocou
Infrastructure —, esta feature ajusta `SaleConfiguration` para introduzir o token de concorrência
`xmin`, necessário para satisfazer FR-013 sem exigir nenhuma migration. O código novo de
Application segue o mesmo padrão de pastas por caso de uso dentro de `Sales/` (`Cancel/`, ao lado
de `Create/`, `Get/`, `List/` e `Update/`), e o novo event handler fica em `Sales/Events/`, ao
lado dos três já existentes. O único ajuste fora do escopo estrito do novo endpoint é a captura
de `DbUpdateConcurrencyException` em `UpdateSaleCommandHandler` (005) — consequência direta de
introduzir o token de concorrência nesta feature: sem esse ajuste, uma alteração (`PUT`)
concorrente com um cancelamento (`DELETE`) da mesma venda passaria a vazar uma exception não
tratada (`500`) em vez do `400` de negócio já esperado, violando o Princípio VII para um cenário
que só passa a ser alcançável a partir desta feature.

## Complexity Tracking

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| Ajuste em `UpdateSaleCommandHandler` (feature 005) | O token de concorrência introduzido para satisfazer FR-013 (006) torna `DbUpdateConcurrencyException` alcançável também pelo `PUT` já existente (corrida contra um `DELETE`); sem capturá-la ali, o Princípio VII (nenhuma exception vazando como erro de negócio) seria violado nesse cenário. | Não introduzir o token de concorrência: rejeitada porque, sem ele, FR-013 não é satisfazível — sem nenhum mecanismo de concorrência otimista hoje, duas requisições de cancelamento concorrentes simplesmente aplicariam a mutação duas vezes em memória e a segunda `SaveChanges` sobrescreveria a primeira silenciosamente, violando a decisão da sessão de clarificação (a que perder a corrida deve receber `400`, nunca sucesso duplicado). |
