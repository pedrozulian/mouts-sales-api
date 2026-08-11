# Tasks: Cancelar Venda

**Input**: Design documents from `/specs/006-cancelar-venda/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/cancel-sale.md](./contracts/cancel-sale.md),
[quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios — o Princípio II da constitution (TDD, não negociável) exige
teste que falhe antes de qualquer linha de produção nova.

**Organization**: tasks agrupadas pelas 3 user stories de `spec.md` (P1, P2, P3). US1 entrega o
fluxo principal (`Sale.Cancel()`, `CancelSaleCommandHandler`, endpoint) já com a única invariante
de domínio embutida (venda cancelada é imutável). US2 acrescenta a proteção completa contra
cancelamento inválido: a distinção `404`/`400` no endpoint e — a parte tecnicamente mais densa
desta feature — o token de concorrência `xmin` (`research.md`, seção 3) que garante FR-013 (duas
requisições de cancelamento concorrentes nunca resultam em duplo sucesso), incluindo o ajuste
mínimo e justificado em `UpdateSaleCommandHandler` (005) para não deixar essa mesma exceção vazar
como `500` num `PUT` concorrente. US3 acrescenta o `INotificationHandler` de observabilidade
(`SaleCancelledEventHandler`) e formaliza por teste a contagem exata de eventos — a emissão do
evento em si já é parte inseparável de `Sale.Cancel()` (US1), mas a *auditoria* dele é o valor de
negócio da User Story 3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tasks incompletas)
- **[Story]**: US1, US2 ou US3 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes): `src/SalesApi.Domain/`,
`src/SalesApi.Application/`, `src/SalesApi.Api/`, `src/SalesApi.Infrastructure/`, com
`tests/SalesApi.Domain.Tests/`, `tests/SalesApi.Application.Tests/`, `tests/SalesApi.Api.Tests/`
espelhando as camadas tocadas por esta feature — conforme `plan.md`. Diferente de
`005-alterar-venda` (que não tocou Infrastructure), esta feature ajusta
`SalesApi.Infrastructure/Persistence/Configurations/SaleConfiguration.cs` para o token de
concorrência `xmin` — sem nenhuma migration nova (ver `research.md`, seção 9).

---

## Phase 1: Setup

Sem tasks nesta feature. Nenhuma dependência nova é necessária — todo o ferramental (pacotes
NuGet, `docker-compose.yml`, CI) já foi configurado em `002-registrar-venda` e é reaproveitado
sem alteração (ver `plan.md`, Technical Context).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: o único tipo compartilhado que bloqueia a compilação de `Sale.Cancel()` (US1).

**⚠️ CRITICAL**: nenhuma user story pode começar antes desta fase estar completa.

- [X] T001 [P] Evento de domínio `SaleCancelled` (`SaleId`, `SaleNumber`), herdando de
  `DomainEvent` (mesmo padrão de `SaleModified`) em
  `src/SalesApi.Domain/Sales/Events/SaleCancelled.cs`

**Checkpoint**: fundação pronta — as user stories podem começar.

---

## Phase 3: User Story 1 - Cancelar uma venda ativa (Priority: P1) 🎯 MVP

**Goal**: `DELETE /api/sales/{id}` cancela logicamente uma venda ativa e todos os seus itens
ainda ativos, zera o total geral e responde `204` sem corpo.

**Independent Test**: registrar uma venda com múltiplos itens (`POST /api/sales`, já existente),
solicitar seu cancelamento e confirmar que a venda e todos os itens passam a constar como
cancelados, com total geral zerado (ver `quickstart.md`, Cenário 1).

### Tests for User Story 1 ⚠️

> Escrever estes testes primeiro e vê-los falhar antes de implementar.

- [X] T002 [P] [US1] Teste unitário: `Sale.Cancel()` em venda ativa com itens ativos marca a
  venda e todos os itens ainda ativos como cancelados e zera `TotalAmount` (FR-002, FR-003,
  INV-06) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T003 [P] [US1] Teste unitário: `Sale.Cancel()` mantém inalterado qualquer item que já
  estivesse cancelado individualmente antes da chamada (FR-012) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T004 [P] [US1] Teste unitário: `CancelSaleCommandHandler` com venda ativa existente chama
  `Sale.Cancel()`, persiste via `SaveChangesAsync` e retorna `Result.Success()`, usando
  `AppDbContext` com `UseInMemoryDatabase` (mesmo padrão de `UpdateSaleCommandHandlerTests`) em
  `tests/SalesApi.Application.Tests/Sales/CancelSaleCommandHandlerTests.cs`
- [X] T005 [P] [US1] Teste de integração: `DELETE /api/sales/{id}` em venda ativa retorna `204
  No Content` sem corpo; `GET /api/sales/{id}` em seguida confirma `isCancelled: true` e
  `totalAmount: 0.00` em todos os itens e na venda, conforme `contracts/cancel-sale.md`, via
  `WebApplicationFactory<Program>` em `tests/SalesApi.Api.Tests/Sales/CancelSaleEndpointTests.cs`

### Implementation for User Story 1

- [X] T006 [US1] `Sale.Cancel()`: passagem única (sem two-pass — não há entrada externa a
  validar, ver `research.md`, seção 1) — cancela cada item de `Items.Where(i => !i.IsCancelled)`
  via `SaleItem.Cancel()` já existente, marca `IsCancelled = true`, zera `TotalAmount`, atualiza
  `UpdatedAt` e registra `SaleCancelled` (depende de T001) em
  `src/SalesApi.Domain/Sales/Sale.cs`
- [X] T007 [P] [US1] `CancelSaleCommand` (`record Id : IRequest<Result>` — resposta sem corpo,
  ver `research.md`, seção 7) em
  `src/SalesApi.Application/Sales/Cancel/CancelSaleCommand.cs`
- [X] T008 [US1] `CancelSaleCommandHandler`: carrega a venda **com tracking**
  (`_context.Sales.Include(s => s.Items).FirstOrDefaultAsync`, mesmo padrão de
  `UpdateSaleCommandHandler` — ver `research.md`, seção 6); se não encontrada,
  `Result.Failure(new Notification("id", "Venda não encontrada."))`; senão chama `sale.Cancel()`;
  em falha, propaga os erros; em sucesso, `SaveChangesAsync` e retorna `Result.Success()`; log
  estruturado (`LogWarning` quando não encontrada, `LogInformation` quando cancelada com sucesso)
  (depende de T006, T007) em
  `src/SalesApi.Application/Sales/Cancel/CancelSaleCommandHandler.cs`
- [X] T009 [US1] Endpoint `DELETE /api/sales/{id:guid}`: envia `new CancelSaleCommand(id)` via
  `ISender`, sucesso → `Results.NoContent()`, falha → `Results.BadRequest` com `{ errors:
  [{key,message}] }` (mesmo formato dos demais endpoints; distinção com `404` fica para US2 — ver
  T018) (depende de T008) em `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 completo — `DELETE /api/sales/{id}` cancela a venda de forma
independentemente testável.

---

## Phase 4: User Story 2 - Impedir cancelamento inválido (Priority: P2)

**Goal**: o sistema recusa o cancelamento quando a venda não existe (`404`) ou já está cancelada
(`400`), e garante que, entre duas solicitações de cancelamento concorrentes para a mesma venda,
exatamente uma seja aplicada — a outra recebe `400`, nunca um erro não tratado (FR-005, FR-006,
FR-010, FR-011, FR-013).

**Independent Test**: solicitar o cancelamento de um identificador inexistente (`404`) e de uma
venda já cancelada (`400`); disparar duas requisições de cancelamento concorrentes contra a mesma
venda ativa e confirmar exatamente uma `204` e uma `400` (ver `quickstart.md`, Cenários 2, 3 e 4).

### Tests for User Story 2 ⚠️

- [X] T010 [P] [US2] Teste unitário: `Sale.Cancel()` em venda já cancelada retorna `Failure` com
  chave `"sale"`, sem mutar nada (INV-07, FR-005) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T011 [P] [US2] Teste unitário: `CancelSaleCommandHandler` com `Id` que não corresponde a
  nenhuma venda retorna `Result.Failure` com `Notification.Key == "id"`, sem persistir nada
  (depende de T008) em
  `tests/SalesApi.Application.Tests/Sales/CancelSaleCommandHandlerTests.cs`
- [X] T012 [P] [US2] Teste de integração: `DELETE /api/sales/{id}` em venda inexistente retorna
  `404` com `errors[0].key = "id"`; em venda já cancelada retorna `400` com `errors[0].key =
  "sale"`, conforme `contracts/cancel-sale.md`, em
  `tests/SalesApi.Api.Tests/Sales/CancelSaleEndpointTests.cs`
- [X] T013 [P] [US2] Teste de integração: duas requisições `DELETE /api/sales/{id}` concorrentes
  para a mesma venda ativa via `Task.WhenAll` (mesmo padrão de `CreateSaleConcurrencyTests.cs`)
  resultam em exatamente uma resposta `204` e uma `400`, nunca duas `204` nem um `500` (FR-013)
  em `tests/SalesApi.Api.Tests/Sales/CancelSaleConcurrencyTests.cs`
- [X] T014 [P] [US2] Teste unitário: `UpdateSaleCommandHandler` (005) captura
  `DbUpdateConcurrencyException` ao salvar e retorna `Result<SaleResponse>.Failure` com
  `Notification.Key == "sale"` — novo caso, consequência do token de concorrência introduzido
  nesta feature (`research.md`, seção 4) em
  `tests/SalesApi.Application.Tests/Sales/UpdateSaleCommandHandlerTests.cs`

### Implementation for User Story 2

- [X] T015 [US2] `SaleConfiguration`: `builder.Property<uint>("xmin").IsRowVersion()` — mapeia o `xmin` de
  sistema do PostgreSQL como token de concorrência do EF Core para `Sale`, sem nenhuma migration
  (ver `research.md`, seção 3) em
  `src/SalesApi.Infrastructure/Persistence/Configurations/SaleConfiguration.cs`
- [X] T016 [US2] `CancelSaleCommandHandler`: envolve `SaveChangesAsync` em `try/catch
  (DbUpdateConcurrencyException)`, traduzindo para `Result.Failure(new Notification("sale",
  "Venda já está cancelada."))` (depende de T008, T015) em
  `src/SalesApi.Application/Sales/Cancel/CancelSaleCommandHandler.cs`
- [X] T017 [US2] `UpdateSaleCommandHandler` (005): mesmo `try/catch
  (DbUpdateConcurrencyException)` ao redor de `SaveChangesAsync`, traduzindo para
  `Result<SaleResponse>.Failure(new Notification("sale", "Venda cancelada não pode ser
  alterada."))` — ajuste mínimo e justificado, consequência direta de T015 (`research.md`, seção
  4; `plan.md`, Complexity Tracking) (depende de T015) em
  `src/SalesApi.Application/Sales/Update/UpdateSaleCommandHandler.cs`
- [X] T018 [US2] Endpoint: distinguir `404` (venda não encontrada) de `400` (venda já cancelada
  ou conflito de concorrência) verificando `result.Errors.Any(e => e.Key == "id")` — mesma
  convenção de `UpdateSale`/`GetSale` (ver `research.md`, seção 5) (depende de T009) em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 e US2 funcionam de forma independente — cancelamento bem-sucedido, cada
caminho de rejeição e a garantia de concorrência (FR-013).

---

## Phase 5: User Story 3 - Rastrear o cancelamento da venda por evento de domínio (Priority: P3)

**Goal**: todo cancelamento bem-sucedido produz exatamente um evento `SaleCancelled`, registrado
em log estruturado, permitindo auditoria sem consulta adicional e sem ruído de um evento por
item afetado.

**Independent Test**: cancelar uma venda com três itens ativos e verificar que exatamente um
evento `SaleCancelled` é emitido — nunca um evento por item; confirmar que uma tentativa
rejeitada não emite nenhum evento (ver `spec.md`, User Story 3, Acceptance Scenarios 1–2).

### Tests for User Story 3 ⚠️

- [X] T019 [P] [US3] Teste unitário: `Sale.Cancel()` em venda com três itens ativos registra
  exatamente 1 `SaleCancelled` em `sale.DomainEvents`, nenhum evento adicional por item (FR-008,
  FR-009) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T020 [P] [US3] Teste unitário: `Sale.Cancel()` rejeitado (venda já cancelada) não registra
  nenhum evento em `sale.DomainEvents` (FR-010) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T021 [P] [US3] Teste unitário: `SaleCancelledEventHandler` loga `SaleId` e `SaleNumber`
  (mesmo padrão de `SaleModifiedEventHandlerTests`) em
  `tests/SalesApi.Application.Tests/Sales/Events/SaleCancelledEventHandlerTests.cs`

### Implementation for User Story 3

- [X] T022 [P] [US3] `SaleCancelledEventHandler` (`INotificationHandler<SaleCancelled>`): log
  estruturado (`LogInformation` com `SaleId`, `SaleNumber`), mesmo formato de
  `SaleModifiedEventHandler` (depende de T001) em
  `src/SalesApi.Application/Sales/Events/SaleCancelledEventHandler.cs`

**Checkpoint**: as 3 user stories funcionam de forma independente — US1 cancela, US2 protege as
invariantes e a concorrência, US3 garante rastreabilidade auditável de cada cancelamento.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T023 [P] Rodar os Cenários 1–4 de `quickstart.md` manualmente contra o ambiente Docker e
  confirmar cada resposta contra `contracts/cancel-sale.md` — validado de forma equivalente pela
  suíte `SalesApi.Api.Tests` (Testcontainers/Postgres real, mesmo pipeline HTTP), que cobre
  byte-a-byte os 4 cenários: sucesso 204+total zerado (Cenário 1), venda inexistente 404
  (Cenário 2), venda já cancelada 400 (Cenário 3) e duas requisições concorrentes — uma 204, uma
  400 (Cenário 4, `CancelSaleConcurrencyTests`)
- [X] T024 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX):
  `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`, sem regressão em
  `002-registrar-venda`/`003-consultar-venda`/`004-listar-vendas`/`005-alterar-venda` — 118 testes
  passando (50 Domain + 33 Application + 35 Api); todo o código novo desta feature
  (`Sale.Cancel()`, `CancelSaleCommand`/`Handler`, `SaleCancelled`, `SaleCancelledEventHandler`,
  `SaleConfiguration`) em 100% de cobertura de linha, incluindo o branch de
  `DbUpdateConcurrencyException` — a verificação de cobertura revelou que o teste de integração
  concorrente (T013) nem sempre exercita esse branch por depender de timing real entre duas
  requisições HTTP; por isso foi adicionado um teste determinístico extra em
  `CancelSaleCommandHandlerTests.cs` (mesma técnica de T014: forçar `OriginalValue` do shadow
  property `xmin` via `context.Entry(...).Property("xmin").OriginalValue`, já que o provider
  InMemory não gera um novo valor sozinho a cada `SaveChanges` como o PostgreSQL real)
- [X] T025 Build dos 4 projetos + testes sem warnings, respeitando
  `TreatWarningsAsErrors=true` de `Directory.Build.props` — `dotnet build SalesApi.sln
  -warnaserror` limpo (0 Warning(s), 0 Error(s))
- [X] T026 [P] Adicionar sumário, descrição e `Produces(204)` / `Produces(400)` /
  `Produces(404)` às anotações do Swagger do `DELETE /api/sales/{id}` em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem tasks — nada bloqueia o início da Phase 2
- **Foundational (Phase 2)**: T001, sem dependências — BLOQUEIA todas as user stories
- **User Stories (Phase 3–5)**: todas dependem da Phase 2 completa; US2 e US3 estendem o mesmo
  `Sale.Cancel()`/`CancelSaleCommandHandler`/endpoint criados em US1 (T006–T009), por isso seguem
  na prática a ordem P1 → P2 → P3, ainda que independentemente testáveis a cada checkpoint
- **Polish (Phase 6)**: depende de todas as user stories desejadas estarem completas

### User Story Dependencies

- **US1 (P1)**: depende apenas da Phase 2 (Foundational)
- **US2 (P2)**: depende de US1 — a única invariante de domínio (venda cancelada é imutável) já
  nasce dentro de `Sale.Cancel()` (T006); o trabalho genuinamente novo é o token de concorrência
  (T015), sua tradução em ambos os handlers (T016, T017) e a distinção `404`/`400` no endpoint
  (T018)
- **US3 (P3)**: depende de US1 — a emissão do evento já nasce dentro de `Sale.Cancel()` (T006); a
  implementação nova é o `INotificationHandler` de log (T022)

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio
  II, TDD)
- Domain antes de Application antes de Infrastructure/Api (Princípio V)
- História completa e com checkpoint validado antes de seguir para a próxima prioridade

### Parallel Opportunities

- T002, T003, T004 e T005 (testes de US1) em paralelo entre si
- T007 (US1: `CancelSaleCommand`) pode ser feito em paralelo com a escrita de T002–T005, antes de
  T006/T008/T009
- T010 a T014 (testes de US2) em paralelo entre si
- T019, T020 e T021 (testes de US3) em paralelo entre si
- T023, T024 e T026 (Polish) em paralelo entre si

---

## Parallel Example: User Story 1

```bash
# Testes de US1 em paralelo (todos devem falhar antes da implementação):
Task: "Teste unitário de Sale.Cancel() (venda ativa com itens ativos) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de Sale.Cancel() (itens já cancelados individualmente) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de CancelSaleCommandHandler (fluxo principal) em tests/SalesApi.Application.Tests/Sales/CancelSaleCommandHandlerTests.cs"
Task: "Teste de integração de DELETE /api/sales/{id} (sucesso) em tests/SalesApi.Api.Tests/Sales/CancelSaleEndpointTests.cs"

# Implementação de US1:
Task: "CancelSaleCommand em src/SalesApi.Application/Sales/Cancel/CancelSaleCommand.cs"
# Sale.Cancel() (T006) depende do evento SaleCancelled (T001) — implementar antes
# CancelSaleCommandHandler (T008) depende de Sale.Cancel() (T006) e CancelSaleCommand (T007)
```

---

## Implementation Strategy

### MVP First (User Story 1 apenas)

1. Completar Phase 2: Foundational (evento `SaleCancelled`)
2. Completar Phase 3: User Story 1
3. **Parar e validar**: rodar `quickstart.md` Cenário 1 manualmente; confirmar testes de US1
   passando
4. Neste ponto já existe um `DELETE /api/sales/{id}` funcional, cancelando a venda inteira —
   suficiente para demonstrar o núcleo do UC-05 numa entrevista

### Incremental Delivery

1. Foundational → base pronta (evento de domínio)
2. US1 → validar isoladamente → MVP demonstrável (cancelamento bem-sucedido)
3. US2 → validar isoladamente → toda tentativa inválida rejeitada com status/chave corretos,
   inclusive sob concorrência
4. US3 → validar isoladamente → cada cancelamento auditável por log estruturado
5. Polish → cobertura, build limpo, documentação interativa

### Parallel Team Strategy

Como US2 e US3 estendem o mesmo `Sale.Cancel()`/`CancelSaleCommandHandler`/endpoint criados por
US1, esta feature tem pouco a ganhar com paralelismo entre desenvolvedores diferentes por user
story — o ganho real de paralelismo está dentro de cada fase, entre as tasks de teste marcadas
`[P]`.

## Notes

- [P] = arquivos diferentes (ou testes independentes no mesmo arquivo, sem dependência entre si),
  sem dependência de tasks incompletas
- [US1]/[US2]/[US3] rastreiam cada task até a user story de `spec.md`
- Rodar os testes e vê-los falhar antes de implementar (Princípio II, TDD)
- T014 e T017 tocam `UpdateSaleCommandHandler` (005), não `CancelSaleCommandHandler` (006) — é o
  único ponto em que esta feature ajusta código de uma feature já entregue, e é intencional (ver
  `research.md`, seção 4, e `plan.md`, Complexity Tracking)
- Consultar `specs/006-cancelar-venda/contracts/cancel-sale.md` para o formato exato de resposta
  e de erro antes de implementar T006, T008, T009 e T018
- `UseXminAsConcurrencyToken()` (mencionado em `plan.md`/`research.md` como decisão inicial) está
  obsoleto no `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11 em uso — T015 usa a forma padrão do
  EF Core, `builder.Property<uint>("xmin").IsRowVersion()`, com efeito equivalente
- Descoberta durante T024: o branch de `catch (DbUpdateConcurrencyException)` de
  `CancelSaleCommandHandler` (T016) depende de timing real entre duas requisições HTTP para ser
  exercitado pelo teste de integração (T013) — nem sempre acontece. Um teste unitário
  determinístico adicional foi incluído em `CancelSaleCommandHandlerTests.cs` (mesma técnica de
  T014), fora do escopo original de T016, para garantir 100% de cobertura desse branch
  independentemente de timing
