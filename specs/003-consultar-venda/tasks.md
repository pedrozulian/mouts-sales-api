# Tasks: Consultar Venda

**Input**: Design documents from `/specs/003-consultar-venda/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/get-sale.md](./contracts/get-sale.md),
[quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios — o Princípio II da constitution (TDD, não negociável)
exige teste que falhe antes de qualquer linha de produção nova. Onde uma user story não exige
nenhuma linha de produção nova (US2, ver Phase 4), os testes ainda são obrigatórios como prova
formal do requisito, mesmo que já nasçam passando a partir do código de US1.

**Organization**: tasks agrupadas pelas 3 user stories de `spec.md` (P1, P2, P3). As três
compartilham o mesmo endpoint (`GET /api/sales/{id}`) e o mesmo `GetSaleQueryHandler`: US1
entrega a leitura básica (venda encontrada) e já comprova que a consulta não tem efeitos
colaterais (FR-009/FR-011/SC-004); US2 comprova que esse mesmo desenho já expõe vendas/itens
cancelados corretamente, sem exigir código novo; US3 endurece o handler e o endpoint para o
caminho de "não encontrada" (404), incluindo identificadores em formato inválido.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tasks incompletas)
- **[Story]**: US1, US2 ou US3 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes): `src/SalesApi.Domain/`,
`src/SalesApi.Application/`, `src/SalesApi.Infrastructure/`, `src/SalesApi.Api/`, com
`tests/SalesApi.Application.Tests/`, `tests/SalesApi.Api.Tests/` espelhando as camadas
tocadas por esta feature — conforme `plan.md`. Nenhum arquivo de `SalesApi.Domain` ou
`SalesApi.Infrastructure` é criado ou alterado nesta feature.

---

## Phase 1: Setup

Sem tasks nesta feature. Nenhuma dependência nova é necessária — todo o ferramental (pacotes
NuGet, `docker-compose.yml`, CI) já foi configurado em `002-registrar-venda` e é reaproveitado
sem alteração.

---

## Phase 2: Foundational (Blocking Prerequisites)

Sem tasks bloqueantes nesta feature. O agregado `Sale`/`SaleItem`, `IApplicationDbContext`,
`AppDbContext`, o `Result`/`Notification` pattern e os DTOs de resposta (`SaleResponse`,
`SaleItemResponse`, `ExternalReferenceResponse`, já mapeados via Mapster) existem desde
`002-registrar-venda` e são reaproveitados sem alteração (ver `research.md`, seções 1 e 3). A
query e o handler específicos desta feature são criados diretamente na Phase 3 (User Story 1),
que funciona como a base compartilhada pelas 3 user stories — mesmo padrão adotado em
`002-registrar-venda`.

---

## Phase 3: User Story 1 - Consultar uma venda existente pelo identificador (Priority: P1) 🎯 MVP

**Goal**: consultar uma venda existente pelo `id` e retornar `200` com a representação
completa (cliente, filial, itens, descontos e totais já calculados), comprovando que a
consulta em si não tem nenhum efeito colateral.

**Independent Test**: registrar uma venda (`POST /api/sales`, já existente), consultá-la pelo
`id` retornado e conferir que todos os campos da resposta batem com o que foi persistido (ver
`quickstart.md`, Cenário 1).

### Tests for User Story 1 ⚠️

> Escrever estes testes primeiro e vê-los falhar antes de implementar.

- [X] T001 [P] [US1] Teste unitário do `GetSaleQueryHandler`: venda existente retorna `Result`
  de sucesso com `SaleResponse` completo (cliente, filial, `totalAmount`, e itens com
  `product.id`/`product.name`, `discountPercentage`/`discountAmount`/`totalAmount` — FR-003,
  FR-004), usando `AppDbContext` com `UseInMemoryDatabase` (mesmo padrão de
  `CreateSaleCommandHandlerTests`) em
  `tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs`
- [X] T002 [P] [US1] Teste de integração: `POST /api/sales` seguido de
  `GET /api/sales/{id}` retorna `200` com corpo idêntico ao registrado — mesmo `saleNumber`,
  `totalAmount`, desconto e total do item, e o `product.id`/`product.name` de cada item
  denormalizados na resposta (FR-004) —, via `WebApplicationFactory<Program>`, conforme
  `contracts/get-sale.md` em `tests/SalesApi.Api.Tests/Sales/GetSaleEndpointTests.cs`
- [X] T003 [P] [US1] Teste unitário do `GetSaleQueryHandler` comprovando FR-009, FR-011 e
  SC-004 (consulta sem efeitos colaterais): usar um `IPublisher` espião (contando chamadas a
  `Publish`) no lugar do `NoOpPublisher` mudo e assertar zero chamadas após `Handle()`; e
  reler a `Sale` diretamente do banco antes e depois da consulta, assertando que nenhum campo
  mudou (nenhuma escrita ocorreu) em
  `tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs`

### Implementation for User Story 1

- [X] T004 [P] [US1] `GetSaleQuery` (`record Id : IRequest<Result<SaleResponse>>`) em
  `src/SalesApi.Application/Sales/Get/GetSaleQuery.cs`
- [X] T005 [US1] `GetSaleQueryHandler`: busca a venda via
  `IApplicationDbContext.Sales.AsNoTracking().Include(s => s.Items)`, mapeia para
  `SaleResponse` via Mapster (reaproveitando o mapeamento já registrado por
  `CreateSaleMappingConfig`) e registra log estruturado (`LogInformation`) quando encontrada
  (depende de T004) em `src/SalesApi.Application/Sales/Get/GetSaleQueryHandler.cs`
- [X] T006 [US1] Endpoint `GET /api/sales/{id:guid}` retornando `200 OK` com o corpo da venda
  no caminho de sucesso (depende de T005) em `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 completo — `GET /api/sales/{id}` consulta uma venda existente, devolve a
representação completa e comprovadamente não altera estado nem dispara eventos, de forma
independentemente testável.

---

## Phase 4: User Story 2 - Consultar uma venda cancelada, total ou parcialmente (Priority: P2)

**Goal**: comprovar que uma venda cancelada (integralmente ou apenas em um item) continua
acessível via consulta, com `isCancelled` correto em cada nível (venda e item) e `totalAmount`
refletindo apenas os itens ativos.

**Independent Test**: preparar uma venda com um item cancelado e uma venda cancelada
integralmente (seed direto no banco — ver `research.md`, seção 6, já que UC-05/UC-06 ainda não
existem) e consultar cada uma, conferindo `isCancelled` e `totalAmount` (ver `quickstart.md`,
Cenário 3).

### Tests for User Story 2 ⚠️

- [X] T007 [P] [US2] Teste unitário: venda ativa com um item cancelado e outro ativo — o item
  cancelado aparece na resposta com `isCancelled: true`, e `totalAmount` da venda considera só
  o item ativo. Estado preparado via
  `context.Entry(item).Property(nameof(SaleItem.IsCancelled)).CurrentValue = true` (sem passar
  por um método `Cancel` que ainda não existe no agregado) em
  `tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs`
- [X] T008 [P] [US2] Teste unitário: venda cancelada integralmente — `isCancelled: true` na
  venda, `totalAmount` zero, itens ainda presentes na resposta cada um com seu próprio
  `isCancelled`. Estado preparado da mesma forma que T007, ajustando também
  `Sale.IsCancelled`/`Sale.TotalAmount` em
  `tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs`
- [X] T009 [P] [US2] Teste de integração: `GET /api/sales/{id}` para uma venda com item
  cancelado e para uma venda cancelada integralmente retornam `200` com `isCancelled` correto
  em cada nível, conforme os exemplos de `contracts/get-sale.md`. Estado preparado obtendo um
  `AppDbContext`/`IApplicationDbContext` via `factory.Services.CreateScope()` e aplicando a
  mesma técnica de T007/T008 em
  `tests/SalesApi.Api.Tests/Sales/GetSaleEndpointTests.cs`

### Implementation for User Story 2

Nenhuma task de implementação nesta fase: `GetSaleQueryHandler` (T005) já lê e mapeia
`Sale`/`SaleItem` sem filtrar nem recalcular nada (FR-005, FR-006, FR-010), então o
comportamento exigido por esta user story já está coberto pelo desenho de US1. Os testes acima
formalizam esse requisito e devem passar sem exigir nenhuma alteração de produção.

**Checkpoint**: US1 e US2 funcionam de forma independente — vendas ativas e canceladas (total
ou parcialmente) são consultáveis, sempre com o estado de cancelamento correto.

---

## Phase 5: User Story 3 - Identificar claramente uma consulta a venda inexistente (Priority: P3)

**Goal**: responder `404` com uma mensagem clara, no mesmo formato de erro da API, quando o
`id` informado não corresponder a nenhuma venda — inclusive quando o valor da rota não é sequer
um identificador válido.

**Independent Test**: consultar um `id` aleatório que não corresponde a nenhuma venda
registrada e confirmar `404` com o corpo de erro padrão (ver `quickstart.md`, Cenário 2).

### Tests for User Story 3 ⚠️

- [X] T010 [P] [US3] Teste unitário: `GetSaleQueryHandler` com `id` inexistente retorna
  `Result` de falha contendo uma `Notification` com `Key == "id"` em
  `tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs`
- [X] T011 [P] [US3] Teste de integração: `GET /api/sales/{id}` com `id` inexistente retorna
  `404 Not Found` com o corpo `{ errors: [{ key: "id", message: "..." }] }`, conforme
  `contracts/get-sale.md`, em `tests/SalesApi.Api.Tests/Sales/GetSaleEndpointTests.cs`
- [X] T012 [P] [US3] Teste de integração: `GET /api/sales/{id}` com um valor de rota que não é
  um `Guid` válido (ex.: `/api/sales/not-a-guid`) retorna `404 Not Found` pelo próprio route
  constraint `{id:guid}` do ASP.NET Core, sem sequer chegar ao `GetSaleQueryHandler` — fixa por
  teste a suposição registrada em `research.md`, seção 5, em
  `tests/SalesApi.Api.Tests/Sales/GetSaleEndpointTests.cs`

### Implementation for User Story 3

- [X] T013 [US3] `GetSaleQueryHandler`: quando a venda não é encontrada, registrar log
  estruturado (`LogWarning`) com o `id` consultado e retornar
  `Result<SaleResponse>.Failure(new Notification("id", "Venda não encontrada."))` (depende de
  T005) em `src/SalesApi.Application/Sales/Get/GetSaleQueryHandler.cs` — implementada junto
  com T005: com `Nullable` habilitado e `TreatWarningsAsErrors=true`, o guard clause para
  `sale is null` é obrigatório para o projeto sequer compilar, então as duas branches do
  `Result` nasceram no mesmo commit. T013 permanece como o ponto em que esse comportamento
  passou a ter cobertura de teste dedicada (T010–T012).
- [X] T014 [US3] Endpoint: traduzir o `Result` de falha da query para `404 Not Found` com o
  corpo `{ errors: [{key,message}] }` (depende de T006, T013) em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs` — mesma observação de T013: implementada junto
  com T006 pelo mesmo motivo (o branch `if (!result.IsSuccess)` é necessário desde o primeiro
  commit do endpoint).

**Checkpoint**: as 3 user stories funcionam de forma independente — US1 consulta vendas
existentes sem efeitos colaterais, US2 expõe corretamente o estado de cancelamento, US3
responde 404 de forma clara para identificadores inexistentes ou malformados.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T015 [P] Rodar os cenários 1–2 de `quickstart.md` manualmente contra o ambiente Docker e
  confirmar cada resposta contra `contracts/get-sale.md` — validado contra a API rodando
  localmente com o PostgreSQL do `docker-compose.yml`: Cenário 1 (`POST` + `GET`) retornou
  `200` com corpo idêntico; Cenário 2 (`id` inexistente) retornou `404` com
  `{"errors":[{"key":"id","message":"Venda não encontrada."}]}`
- [X] T016 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX):
  `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"` — 56/56 testes passando (27
  Domain + 10 Application + 19 Api, incluindo os 10 novos desta feature), sem regressão em
  `002-registrar-venda`; `GetSaleQuery.cs` e `GetSaleQueryHandler.cs` (Application) e o novo
  handler `GetSale` em `SalesEndpoints.cs` (Api) com 100% de cobertura de linha, ambas as
  branches (encontrada/não encontrada) exercitadas pelos testes de US1/US3
- [X] T017 Build dos 4 projetos + testes sem warnings, respeitando
  `TreatWarningsAsErrors=true` de `Directory.Build.props` — `dotnet build SalesApi.sln`:
  Build succeeded, 0 Warning(s), 0 Error(s)
- [X] T018 [P] Adicionar sumário, descrição e `Produces<SaleResponse>(200)` /
  `Produces(404)` às anotações do Swagger do `GET /api/sales/{id}` em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs` — implementada junto com T006, no mesmo
  `MapGet` que registra o endpoint (mesmo padrão do `MapPost` de `002-registrar-venda`)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** e **Foundational (Phase 2)**: sem tasks — nada bloqueia o início da
  Phase 3
- **User Stories (Phase 3–5)**: todas dependem de US1 existir, já que compartilham o mesmo
  `GetSaleQueryHandler`/endpoint criados em T004–T006; US2 não edita nenhum arquivo de
  produção, US3 edita os mesmos arquivos de US1 para adicionar o caminho de "não encontrada" —
  por isso, na prática, seguem a ordem P1 → P2 → P3 nesta feature, ainda que
  independentemente testáveis a cada checkpoint
- **Polish (Phase 6)**: depende de todas as user stories desejadas estarem completas

### User Story Dependencies

- **US1 (P1)**: sem dependências de outra user story
- **US2 (P2)**: depende de US1 (reaproveita `GetSaleQueryHandler` sem alteração); nenhuma
  dependência de código nova, apenas de teste
- **US3 (P3)**: depende de US1 (estende `GetSaleQueryHandler` e o endpoint criados em T005/T006
  para adicionar o caminho de falha)

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio
  II, TDD) — em US2, "falhar antes" não se aplica por não haver implementação nova; os testes
  ainda são obrigatórios como prova formal do requisito
- Application antes de Api (Princípio V) — Domain e Infrastructure não são tocados nesta
  feature
- História completa e com checkpoint validado antes de seguir para a próxima prioridade

### Parallel Opportunities

- T001, T002 e T003 (testes de US1) em paralelo
- T004 (US1: `GetSaleQuery`) pode ser feito em paralelo com a escrita de T001–T003, antes de
  T005
- T007, T008 e T009 (testes de US2) em paralelo entre si
- T010, T011 e T012 (testes de US3) em paralelo entre si
- T015, T016 e T018 (Polish) em paralelo entre si

---

## Parallel Example: User Story 1

```bash
# Testes de US1 em paralelo (todos devem falhar antes da implementação):
Task: "Teste unitário de GetSaleQueryHandler (sucesso) em tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs"
Task: "Teste de integração de GET /api/sales/{id} (sucesso) em tests/SalesApi.Api.Tests/Sales/GetSaleEndpointTests.cs"
Task: "Teste unitário de GetSaleQueryHandler (sem efeitos colaterais) em tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs"

# Implementação de US1:
Task: "GetSaleQuery em src/SalesApi.Application/Sales/Get/GetSaleQuery.cs"
# GetSaleQueryHandler (T005) depende de GetSaleQuery (T004) — não roda em paralelo com ele
```

---

## Implementation Strategy

### MVP First (User Story 1 apenas)

1. Completar Phase 3 (US1) — Setup e Foundational não têm tasks nesta feature
2. **Parar e validar**: rodar `quickstart.md` Cenário 1 manualmente; confirmar testes de US1
   passando
3. Neste ponto já existe um `GET /api/sales/{id}` funcional para vendas existentes — suficiente
   para demonstrar o núcleo do UC-02 numa entrevista

### Incremental Delivery

1. US1 → validar isoladamente → MVP demonstrável (consulta básica, sem efeitos colaterais)
2. US2 → validar isoladamente → confirma que vendas/itens cancelados continuam consultáveis
   (sem código novo, só prova formal)
3. US3 → validar isoladamente → API responde 404 de forma clara para identificadores
   inexistentes ou malformados
4. Polish → cobertura, build limpo, documentação interativa

### Parallel Team Strategy

Como US2 não introduz código de produção e US3 edita os mesmos arquivos criados por US1, esta
feature tem pouco a ganhar com paralelismo entre desenvolvedores diferentes por user story — o
ganho real de paralelismo está dentro de cada fase, entre as tasks de teste marcadas `[P]`.

## Notes

- [P] = arquivos diferentes (ou testes independentes no mesmo arquivo, sem dependência entre
  si), sem dependência de tasks incompletas
- [US1]/[US2]/[US3] rastreiam cada task até a user story de `spec.md`
- Rodar os testes e vê-los falhar antes de implementar, exceto em US2 onde não há
  implementação nova (Princípio II, TDD)
- Nenhuma alteração em `SalesApi.Domain` ou `SalesApi.Infrastructure` nesta feature — apenas
  Application (`Sales/Get/`) e Api (`SalesEndpoints.cs`)
- Consultar `specs/003-consultar-venda/contracts/get-sale.md` para o formato exato de resposta
  e de erro antes de implementar T005, T006, T013 e T014
