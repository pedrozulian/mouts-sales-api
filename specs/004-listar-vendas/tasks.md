# Tasks: Listar Vendas

**Input**: Design documents from `/specs/004-listar-vendas/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/list-sales.md](./contracts/list-sales.md),
[quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios — o Princípio II da constitution (TDD, não negociável)
exige teste que falhe antes de qualquer linha de produção nova. Onde uma user story não exige
nenhuma linha de produção nova (US3, ver Phase 5), os testes ainda são obrigatórios como prova
formal do requisito, mesmo que já nasçam passando a partir do código de US1/US2.

**Organization**: tasks agrupadas pelas 3 user stories de `spec.md` (P1, P2, P3). As três
compartilham o mesmo endpoint (`GET /api/sales`) e o mesmo `ListSalesQueryHandler`: US1 entrega
a paginação básica sem filtros (ordenação, metadados de página, forma resumida sem `items`);
US2 estende o mesmo handler com os três filtros combináveis; US3 endurece o handler e o
endpoint para os caminhos de lista vazia e parâmetro inválido, que já nascem parcialmente
cobertos pela validação introduzida em US1/US2 (necessária para `Result<T>` compilar desde o
início — mesmo padrão adotado em `003-consultar-venda`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tasks incompletas)
- **[Story]**: US1, US2 ou US3 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes): `src/SalesApi.Domain/`,
`src/SalesApi.Application/`, `src/SalesApi.Infrastructure/`, `src/SalesApi.Api/`, com
`tests/SalesApi.Application.Tests/`, `tests/SalesApi.Api.Tests/` espelhando as camadas tocadas
por esta feature — conforme `plan.md`. `SalesApi.Domain` não é alterado; diferente de
`003-consultar-venda`, `SalesApi.Infrastructure` recebe uma alteração pontual (índices).

---

## Phase 1: Setup

Sem tasks nesta feature. Nenhuma dependência nova é necessária — todo o ferramental (pacotes
NuGet, `docker-compose.yml`, CI) já foi configurado em `002-registrar-venda` e é reaproveitado
sem alteração (ver `plan.md`, Technical Context).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: infraestrutura e DTOs compartilhados pelas 3 user stories — nenhuma delas retorna
uma resposta válida sem isso.

**⚠️ CRITICAL**: nenhuma user story pode começar antes desta fase estar completa.

- [X] T001 [P] Adicionar `HasIndex` para `customer_id`, `branch_id` (dentro dos `OwnsOne` de
  `Customer`/`Branch`) e `sale_date` em `SaleConfiguration`, e gerar a migration EF Core
  correspondente (`dotnet ef migrations add AddSalesListIndexes --project
  src/SalesApi.Infrastructure --startup-project src/SalesApi.Api`) — conforme `research.md`,
  seção 5 e `data-model.md`, seção "Índices adicionados" — em
  `src/SalesApi.Infrastructure/Persistence/Configurations/SaleConfiguration.cs` e
  `src/SalesApi.Infrastructure/Persistence/Migrations/`
- [X] T002 [P] `PagedResult<T>` genérico (`Items`, `Page`, `PageSize`, `TotalCount`,
  `TotalPages`) com factory `Create(items, page, pageSize, totalCount)` calculando
  `TotalPages = totalCount == 0 ? 0 : Ceiling(totalCount / pageSize)` — conforme `research.md`,
  seção 7 — em `src/SalesApi.Application/Common/Dtos/PagedResult.cs`
- [X] T003 [P] `SaleSummaryResponse` (`Id`, `SaleNumber`, `SaleDate`, `Customer`, `Branch`,
  `TotalAmount`, `IsCancelled` — sem `Items`, FR-004) em
  `src/SalesApi.Application/Sales/Dtos/SaleSummaryResponse.cs`
- [X] T004 `ListSalesMappingConfig` (`IRegister`) registrando `Sale -> SaleSummaryResponse`
  para viabilizar `ProjectToType` sobre `IQueryable<Sale>` (depende de T003 — ver
  `research.md`, seção 3) em `src/SalesApi.Application/Sales/List/ListSalesMappingConfig.cs`

**Checkpoint**: fundação pronta — as user stories podem começar.

---

## Phase 3: User Story 1 - Listar vendas paginadas em ordem cronológica (Priority: P1) 🎯 MVP

**Goal**: `GET /api/sales` sem filtros retorna uma página de vendas em forma resumida (sem
`items`), ordenada por `saleDate` decrescente com desempate por `id`, acompanhada dos metadados
de paginação.

**Independent Test**: registrar várias vendas com datas diferentes (`POST /api/sales`, já
existente), listar sem parâmetros e conferir ordenação, forma resumida e metadados de
paginação contra o total registrado (ver `quickstart.md`, Cenários 1–2).

### Tests for User Story 1 ⚠️

> Escrever estes testes primeiro e vê-los falhar antes de implementar.

- [X] T005 [P] [US1] Teste unitário do `ListSalesQueryHandler`: sem parâmetros, aplica
  `page=1`/`pageSize=20` por padrão, ordena por `SaleDate` decrescente com desempate por `Id`
  (duas vendas com a mesma `saleDate`), e cada item da resposta não expõe `items` (FR-001,
  FR-003, FR-004), usando `AppDbContext` com `UseInMemoryDatabase` (mesmo padrão de
  `GetSaleQueryHandlerTests`) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T006 [P] [US1] Teste unitário do `ListSalesQueryHandler`: `page`/`pageSize` explícitos
  dentro do limite retornam exatamente a fatia esperada, com `totalCount`/`totalPages`
  calculados sobre o total real de vendas (FR-005) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T007 [P] [US1] Teste de integração: `GET /api/sales` sem parâmetros retorna `200` com o
  envelope `{ items, page, pageSize, totalCount, totalPages }`, cada item sem `items` aninhado,
  conforme `contracts/list-sales.md`, via `WebApplicationFactory<Program>` em
  `tests/SalesApi.Api.Tests/Sales/ListSalesEndpointTests.cs`

### Implementation for User Story 1

- [X] T008 [P] [US1] `ListSalesQuery` (`record Page, PageSize, CustomerId, BranchId,
  IsCancelled : IRequest<Result<PagedResult<SaleSummaryResponse>>>`, todos os cinco campos
  `string?` — ver `research.md`, seção 1) em
  `src/SalesApi.Application/Sales/List/ListSalesQuery.cs` (depende de T002, T003)
- [X] T009 [US1] `ListSalesQueryHandler`: parseia e valida `page` (padrão `"1"`, inteiro ≥ 1) e
  `pageSize` (padrão `"20"`, inteiro entre 1 e 100), acumulando um `Notification` por parâmetro
  inválido (mesmo padrão de acumulação de `Sale.Create`); monta `IQueryable<Sale>` via
  `IApplicationDbContext.Sales.AsNoTracking()`, ordena por
  `OrderByDescending(SaleDate).ThenByDescending(Id)`, executa `CountAsync`, aplica
  `Skip((page-1)*pageSize).Take(pageSize)` e projeta com
  `.ProjectToType<SaleSummaryResponse>()` (FR-001 a FR-005); registra log estruturado
  (`LogInformation` com total encontrado) (depende de T004, T008) em
  `src/SalesApi.Application/Sales/List/ListSalesQueryHandler.cs`
- [X] T010 [US1] Endpoint `GET /api/sales`: parâmetros `page`, `pageSize`, `customerId`,
  `branchId`, `isCancelled` como `string?` (ver `research.md`, seção 1), envia `ListSalesQuery`
  e traduz `Result` de sucesso para `200 OK` com o `PagedResult`, e de falha para
  `400 Bad Request` com `{ errors: [{key,message}] }` (mesmo formato de `CreateSale`) (depende
  de T009) em `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 completo — `GET /api/sales` lista vendas paginadas, ordenadas e em forma
resumida, de forma independentemente testável.

---

## Phase 4: User Story 2 - Filtrar vendas por cliente, filial e situação de cancelamento (Priority: P2)

**Goal**: `GET /api/sales` aceita `customerId`, `branchId` e `isCancelled` opcionais,
combináveis por `E` lógico, restringindo o resultado da paginação sem alterar o desenho de US1.

**Independent Test**: registrar vendas de clientes/filiais diferentes e uma venda com estado
cancelado (seed direto no banco — ver `research.md`, seção 6), listar aplicando cada filtro
isoladamente e em combinação, e conferir que só as vendas esperadas aparecem (ver
`quickstart.md`, Cenário 3).

### Tests for User Story 2 ⚠️

- [X] T011 [P] [US2] Teste unitário: `customerId` informado retorna somente vendas daquele
  cliente, com `totalCount`/`totalPages` recalculados sobre o subconjunto filtrado (FR-006,
  FR-009) em `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T012 [P] [US2] Teste unitário: `branchId` informado retorna somente vendas daquela filial
  (FR-007) em `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T013 [P] [US2] Teste unitário: `isCancelled=true`/`isCancelled=false` retornam somente
  vendas com a situação correspondente; ausência do parâmetro retorna ativas e canceladas
  juntas (FR-008). Estado cancelado preparado via
  `context.Entry(sale).Property(nameof(Sale.IsCancelled)).CurrentValue = true` (sem passar por
  um método `Cancel` que ainda não existe no agregado — mesmo padrão de
  `003-consultar-venda/research.md`, seção 6) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T014 [P] [US2] Teste unitário: `customerId` + `branchId` + `isCancelled` informados
  simultaneamente retornam apenas as vendas que atendem a todos os filtros ao mesmo tempo
  (FR-009) em `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T015 [P] [US2] Teste de integração: `GET /api/sales?customerId=...&branchId=...` e
  `GET /api/sales?isCancelled=...` retornam `200` com o subconjunto esperado, conforme
  `contracts/list-sales.md`, em `tests/SalesApi.Api.Tests/Sales/ListSalesEndpointTests.cs`

### Implementation for User Story 2

- [X] T016 [US2] `ListSalesQueryHandler`: parseia `customerId`/`branchId` (`Guid`, quando
  informados) e `isCancelled` (`bool`, quando informado), acumulando um `Notification` por
  parâmetro em formato inválido (mesmo mecanismo de T009); aplica `Where` condicionais
  encadiáveis ao `IQueryable<Sale>` antes da contagem/paginação, combinando-os por `E` lógico
  (FR-006 a FR-009 — ver `research.md`, seção 4) (depende de T009) em
  `src/SalesApi.Application/Sales/List/ListSalesQueryHandler.cs`

**Checkpoint**: US1 e US2 funcionam de forma independente — listagem paginada com e sem
filtros, combináveis entre si.

---

## Phase 5: User Story 3 - Lidar com listagens sem resultado e parâmetros inválidos (Priority: P3)

**Goal**: `GET /api/sales` responde `200` com lista vazia quando nenhuma venda atende aos
filtros ou a página solicitada está além do total, e `400` no envelope padrão de erro quando
`page`, `pageSize`, `customerId`, `branchId` ou `isCancelled` estão em formato ou faixa
inválidos — inclusive múltiplos parâmetros inválidos simultaneamente.

**Independent Test**: listar com um filtro sem correspondência e com uma página muito além do
total, confirmando lista vazia com `200`; e separadamente, listar com parâmetros inválidos
(isolados e combinados), confirmando `400` com uma `Notification` por parâmetro (ver
`quickstart.md`, Cenários 4–5).

### Tests for User Story 3 ⚠️

- [X] T017 [P] [US3] Teste unitário: filtro sem nenhuma venda correspondente retorna
  `items: []`, `totalCount: 0`, `totalPages: 0` (FR-010) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T018 [P] [US3] Teste unitário: `page` além do total de páginas existentes retorna
  `items: []` com sucesso, nunca erro (FR-010) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T019 [P] [US3] Teste unitário: `page="0"`/`page="-1"` e `pageSize="0"` retornam
  `Result.Failure` com `Notification.Key == "page"`/`"pageSize"` respectivamente (FR-011) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T020 [P] [US3] Teste unitário: `pageSize="101"` retorna `Result.Failure` com
  `Notification.Key == "pageSize"` (FR-002, FR-011) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T021 [P] [US3] Teste unitário: `customerId`/`branchId`/`isCancelled` em formato inválido
  (isolados e combinados na mesma chamada) retornam `Result.Failure` com uma `Notification` por
  parâmetro inválido, todas acumuladas na mesma resposta (FR-011) em
  `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs`
- [X] T022 [P] [US3] Teste de integração: `GET /api/sales` com parâmetros inválidos (isolados e
  combinados) retorna `400` com `{ errors: [{key,message}] }`, conforme
  `contracts/list-sales.md`, em `tests/SalesApi.Api.Tests/Sales/ListSalesEndpointTests.cs`

### Implementation for User Story 3

Nenhuma task de implementação nesta fase: a validação de `page`/`pageSize` (T009) e de
`customerId`/`branchId`/`isCancelled` (T016), assim como a tradução de `Result` de falha para
`400` no endpoint (T010), já nasceram junto com US1/US2 — `Result<T>` exige as duas branches
(sucesso/falha) desde o primeiro commit do handler e do endpoint para compilar, mesmo padrão já
observado em `003-consultar-venda/tasks.md` (T013/T014). Os testes acima formalizam e travam
por teste esse comportamento, incluindo o caso de lista vazia (que não exige nenhum branch de
erro — apenas `items` vazio dentro de um `Result` de sucesso).

**Checkpoint**: as 3 user stories funcionam de forma independente — US1 pagina e ordena, US2
filtra, US3 responde de forma previsível tanto para "sem resultado" quanto para parâmetro
inválido.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T023 [P] Rodar os cenários 1–5 de `quickstart.md` manualmente contra o ambiente Docker
  (incluindo a migration `AddSalesListIndexes` aplicada) e confirmar cada resposta contra
  `contracts/list-sales.md`
- [X] T024 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX):
  `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`, sem regressão em
  `002-registrar-venda`/`003-consultar-venda`
- [X] T025 Build dos 4 projetos + testes sem warnings, respeitando
  `TreatWarningsAsErrors=true` de `Directory.Build.props`
- [X] T026 [P] Adicionar sumário, descrição e `Produces<PagedResult<SaleSummaryResponse>>(200)`
  / `Produces(400)` às anotações do Swagger do `GET /api/sales` em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem tasks — nada bloqueia o início da Phase 2
- **Foundational (Phase 2)**: T001–T004, sem dependência entre si exceto T004→T003 — BLOQUEIA
  todas as user stories
- **User Stories (Phase 3–5)**: todas dependem da Phase 2 completa; US2 e US3 estendem o mesmo
  `ListSalesQueryHandler`/endpoint criados em US1 (T008–T010), por isso seguem na prática a
  ordem P1 → P2 → P3, ainda que independentemente testáveis a cada checkpoint
- **Polish (Phase 6)**: depende de todas as user stories desejadas estarem completas

### User Story Dependencies

- **US1 (P1)**: depende apenas da Phase 2 (Foundational)
- **US2 (P2)**: depende de US1 (estende `ListSalesQueryHandler` criado em T009 com os filtros,
  T016); nenhum novo arquivo de produção, apenas extensão do handler existente
- **US3 (P3)**: depende de US1 e US2 (a validação que ela testa já foi implementada em T009 e
  T016); nenhuma task de implementação nova

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio
  II, TDD) — em US3, "falhar antes" não se aplica por não haver implementação nova; os testes
  ainda são obrigatórios como prova formal do requisito
- Application antes de Api (Princípio V) — Domain não é tocado nesta feature; Infrastructure
  (T001) é independente e pode ser feita em paralelo com o restante da Phase 2
- História completa e com checkpoint validado antes de seguir para a próxima prioridade

### Parallel Opportunities

- T001, T002 e T003 (Foundational) em paralelo entre si; T004 depende de T003
- T005, T006 e T007 (testes de US1) em paralelo entre si
- T008 (US1: `ListSalesQuery`) pode ser feito em paralelo com a escrita de T005–T007, antes de
  T009
- T011, T012, T013, T014 e T015 (testes de US2) em paralelo entre si
- T017, T018, T019, T020, T021 e T022 (testes de US3) em paralelo entre si
- T023, T024 e T026 (Polish) em paralelo entre si

---

## Parallel Example: User Story 1

```bash
# Testes de US1 em paralelo (todos devem falhar antes da implementação):
Task: "Teste unitário de ListSalesQueryHandler (paginação padrão, ordenação, desempate) em tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs"
Task: "Teste unitário de ListSalesQueryHandler (page/pageSize explícitos) em tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs"
Task: "Teste de integração de GET /api/sales (sucesso, sem filtros) em tests/SalesApi.Api.Tests/Sales/ListSalesEndpointTests.cs"

# Implementação de US1:
Task: "ListSalesQuery em src/SalesApi.Application/Sales/List/ListSalesQuery.cs"
# ListSalesQueryHandler (T009) depende de ListSalesQuery (T008) — não roda em paralelo com ele
```

---

## Implementation Strategy

### MVP First (User Story 1 apenas)

1. Completar Phase 2: Foundational (índices, `PagedResult<T>`, `SaleSummaryResponse`,
   `ListSalesMappingConfig`)
2. Completar Phase 3: User Story 1
3. **Parar e validar**: rodar `quickstart.md` Cenários 1–2 manualmente; confirmar testes de US1
   passando
4. Neste ponto já existe um `GET /api/sales` funcional, paginado e ordenado — suficiente para
   demonstrar o núcleo do UC-03 numa entrevista

### Incremental Delivery

1. Foundational → base pronta (índices, DTOs, mapeamento)
2. US1 → validar isoladamente → MVP demonstrável (listagem paginada e ordenada)
3. US2 → validar isoladamente → filtros por cliente/filial/cancelamento, combináveis
4. US3 → validar isoladamente → lista vazia e parâmetros inválidos tratados de forma previsível
5. Polish → cobertura, build limpo, documentação interativa

### Parallel Team Strategy

Como US2 e US3 estendem o mesmo `ListSalesQueryHandler`/endpoint criados por US1, esta feature
tem pouco a ganhar com paralelismo entre desenvolvedores diferentes por user story — o ganho
real de paralelismo está dentro de cada fase, entre as tasks de teste marcadas `[P]`, e entre os
itens independentes da Phase 2 (T001 de Infrastructure pode andar em paralelo com T002/T003 de
Application).

## Notes

- [P] = arquivos diferentes (ou testes independentes no mesmo arquivo, sem dependência entre
  si), sem dependência de tasks incompletas
- [US1]/[US2]/[US3] rastreiam cada task até a user story de `spec.md`
- Rodar os testes e vê-los falhar antes de implementar, exceto em US3 onde não há
  implementação nova (Princípio II, TDD)
- Única alteração fora de Application/Api nesta feature: `SalesApi.Infrastructure` (T001,
  índices) — `SalesApi.Domain` não é tocado
- Consultar `specs/004-listar-vendas/contracts/list-sales.md` para o formato exato de resposta
  e de erro antes de implementar T009, T010 e T016
