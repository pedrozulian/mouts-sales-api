# Tasks: Alterar Venda

**Input**: Design documents from `/specs/005-alterar-venda/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/update-sale.md](./contracts/update-sale.md),
[quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios — o Princípio II da constitution (TDD, não negociável)
exige teste que falhe antes de qualquer linha de produção nova.

**Organization**: tasks agrupadas pelas 3 user stories de `spec.md` (P1, P2, P3). As três
compartilham o mesmo agregado `Sale.Update` e o mesmo `UpdateSaleCommandHandler`/endpoint: US1
entrega a reconciliação em si (atualizar, adicionar, cancelar implicitamente) já com todas as
invariantes embutidas — o padrão *two-pass* (`research.md`, seção 1) exige que toda validação
exista desde o primeiro commit do método, do mesmo jeito que `Sale.Create` já nasceu validando
tudo em `002-registrar-venda`. US2 acrescenta a distinção `404`/`400` no endpoint (a única peça
genuinamente nova fora do dominio) e formaliza por teste cada caminho de rejeição que o
two-pass de US1 já cobre. US3 acrescenta os dois `INotificationHandler` de observabilidade
(`SaleModifiedEventHandler`, `ItemCancelledEventHandler`) e formaliza por teste a contagem
exata de eventos emitidos pela reconciliação — a emissão dos eventos em si já é parte
inseparável de `Sale.Update` (US1), mas a *auditoria* deles é o valor de negócio da User Story 3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tasks incompletas)
- **[Story]**: US1, US2 ou US3 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes): `src/SalesApi.Domain/`,
`src/SalesApi.Application/`, `src/SalesApi.Api/`, com `tests/SalesApi.Domain.Tests/`,
`tests/SalesApi.Application.Tests/`, `tests/SalesApi.Api.Tests/` espelhando as camadas tocadas
por esta feature — conforme `plan.md`. Diferente de `003-consultar-venda` e
`004-listar-vendas`, esta feature toca o `SalesApi.Domain` (primeiro método de escrita do
agregado além de `Create`). `SalesApi.Infrastructure` não é alterado — nenhuma migration nova
(ver `research.md`, seção 6).

---

## Phase 1: Setup

Sem tasks nesta feature. Nenhuma dependência nova é necessária — todo o ferramental (pacotes
NuGet, `docker-compose.yml`, CI) já foi configurado em `002-registrar-venda` e é reaproveitado
sem alteração (ver `plan.md`, Technical Context).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: tipos de apoio (value object de entrada, eventos de domínio, DTO de item e
mapeamento) compartilhados pelas 3 user stories — nenhuma delas compila sem isso.

**⚠️ CRITICAL**: nenhuma user story pode começar antes desta fase estar completa.

- [X] T001 [P] `SaleItemChangeInput` (`record Id?, Product, Quantity, UnitPrice`), análogo a
  `SaleItemInput` acrescido de `Id` opcional (ver `data-model.md`, seção
  "`SaleItemChangeInput`") em `src/SalesApi.Domain/Sales/SaleItemChangeInput.cs`
- [X] T002 [P] Evento de domínio `SaleModified` (`SaleId`, `SaleNumber`, `TotalAmount`),
  herdando de `DomainEvent` (mesmo padrão de `SaleCreated`) em
  `src/SalesApi.Domain/Sales/Events/SaleModified.cs`
- [X] T003 [P] Evento de domínio `ItemCancelled` (`SaleId`, `SaleItemId`, `ProductId`,
  `Quantity`), herdando de `DomainEvent` em
  `src/SalesApi.Domain/Sales/Events/ItemCancelled.cs`
- [X] T004 [P] `SaleItemChangeRequest` (`record Id?, Product: ExternalReferenceRequest,
  Quantity, UnitPrice`) em `src/SalesApi.Application/Sales/Dtos/SaleItemChangeRequest.cs`
- [X] T005 `UpdateSaleMappingConfig` (`IRegister`) registrando `SaleItemChangeRequest ->
  SaleItemChangeInput` (conversão de tipo aninhado, mesmo motivo de `SaleItemRequest ->
  SaleItemInput` em `CreateSaleMappingConfig` — ver `research.md`, seção 7) (depende de T001,
  T004) em `src/SalesApi.Application/Sales/Update/UpdateSaleMappingConfig.cs`

**Checkpoint**: fundação pronta — as user stories podem começar.

---

## Phase 3: User Story 1 - Alterar cabeçalho e reconciliar itens de uma venda ativa (Priority: P1) 🎯 MVP

**Goal**: `PUT /api/sales/{id}` substitui o cabeçalho (cliente, filial, data) de uma venda ativa
e reconcilia seus itens — atualizar item existente, adicionar item novo, cancelar
implicitamente item ausente — recalculando desconto/total por item e o total geral.

**Independent Test**: registrar uma venda com múltiplos itens (`POST /api/sales`, já existente),
enviar um `PUT` que atualiza a quantidade de um item, adiciona um item novo e omite um item
existente, e conferir que a venda resultante reflete exatamente essas três reconciliações (ver
`quickstart.md`, Cenário 1).

### Tests for User Story 1 ⚠️

> Escrever estes testes primeiro e vê-los falhar antes de implementar.

- [X] T006 [P] [US1] Teste unitário: `Sale.Update` com um item existente referenciado por
  `id` e nova quantidade atualiza o item, recalcula seu desconto/total e o total geral
  (FR-003, FR-006) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T007 [P] [US1] Teste unitário: `Sale.Update` com um item sem `id` adiciona esse item
  como novo, calcula seu desconto/total e o inclui no total geral (FR-005) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T008 [P] [US1] Teste unitário: `Sale.Update` com um item ativo ausente do corpo cancela
  esse item implicitamente (`IsCancelled = true`, nunca removido), retirando-o do total geral
  (FR-006, FR-007, INV-06) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T009 [P] [US1] Teste unitário: `Sale.Update` com novo `customer`/`branch`/`saleDate`
  atualiza o cabeçalho sem afetar itens que não mudaram (FR-001) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T010 [P] [US1] Teste unitário: `UpdateSaleCommandHandler` com venda existente e comando
  válido chama `Sale.Update`, persiste via `SaveChangesAsync` e retorna `SaleResponse` com os
  itens reconciliados, usando `AppDbContext` com `UseInMemoryDatabase` (mesmo padrão de
  `CreateSaleCommandHandlerTests`) em
  `tests/SalesApi.Application.Tests/Sales/UpdateSaleCommandHandlerTests.cs`
- [X] T011 [P] [US1] Teste de integração: `PUT /api/sales/{id}` com reconciliação completa
  (atualizar + adicionar + cancelar implícito) retorna `200` com a venda atualizada, conforme
  `contracts/update-sale.md`, via `WebApplicationFactory<Program>` em
  `tests/SalesApi.Api.Tests/Sales/UpdateSaleEndpointTests.cs`

### Implementation for User Story 1

- [X] T012 [US1] `SaleItem.ApplyChange(int quantity, decimal unitPrice)` (revalida INV-02,
  recalcula `DiscountPercentage`/`DiscountAmount`/`TotalAmount`) e `SaleItem.Cancel()`
  (`IsCancelled = true`) — nenhum dos dois altera `Product` (ver `data-model.md`, seção
  "`SaleItem` — novos métodos") em `src/SalesApi.Domain/Sales/SaleItem.cs`
- [X] T013 [US1] `Sale.Update(ExternalReference customer, ExternalReference branch, DateTime?
  saleDate, IReadOnlyCollection<SaleItemChangeInput> items)`: two-pass validar-então-mutar
  (`research.md`, seção 1) cobrindo venda cancelada (INV-07), cabeçalho obrigatório incl.
  `saleDate`, corpo sem item (INV-01), `id` de item inexistente ou já cancelado, produto imutável em
  item existente, quantidade 1–20 (INV-02), produto duplicado (INV-03); quando válido, atualiza
  cabeçalho, aplica `ApplyChange`/adiciona via `SaleItem.Create`/cancela via `Cancel`, recalcula
  `TotalAmount` (INV-06), atualiza `UpdatedAt`, registra um `ItemCancelled` por item cancelado
  seguido de um único `SaleModified` (ver `data-model.md`, tabela de regras de `Update`)
  (depende de T001, T002, T003, T012) em `src/SalesApi.Domain/Sales/Sale.cs`
- [X] T014 [P] [US1] `UpdateSaleCommand` (`record Id, SaleDate?, Customer:
  ExternalReferenceRequest, Branch: ExternalReferenceRequest, Items:
  IReadOnlyCollection<SaleItemChangeRequest> : IRequest<Result<SaleResponse>>`) (depende de T004)
  em `src/SalesApi.Application/Sales/Update/UpdateSaleCommand.cs`
- [X] T015 [US1] `UpdateSaleCommandHandler`: carrega a venda **com tracking**
  (`_context.Sales.Include(s => s.Items).FirstOrDefaultAsync`, sem `AsNoTracking` — ver
  `research.md`, seção 3); se não encontrada, `Result<SaleResponse>.Failure(new
  Notification("id", "Venda não encontrada."))`; senão adapta `Items` para
  `SaleItemChangeInput` e chama `sale.Update(...)`; em falha, propaga os erros; em sucesso,
  `SaveChangesAsync` e retorna `sale.Adapt<SaleResponse>()`; log estruturado (`LogWarning`
  quando não encontrada, `LogInformation` quando alterada) (depende de T005, T013, T014) em
  `src/SalesApi.Application/Sales/Update/UpdateSaleCommandHandler.cs`
- [X] T016 [P] [US1] `UpdateSaleRequest` (`record SaleDate?, Customer: ExternalReferenceRequest,
  Branch: ExternalReferenceRequest, Items: IReadOnlyCollection<SaleItemChangeRequest>`) (depende
  de T004) em `src/SalesApi.Application/Sales/Dtos/UpdateSaleRequest.cs`
- [X] T017 [US1] Endpoint `PUT /api/sales/{id:guid}`: liga `UpdateSaleRequest`, monta
  `request.Adapt<UpdateSaleCommand>() with { Id = id }`, envia via `ISender`, sucesso →
  `Results.Ok`, falha → `Results.BadRequest` com `{ errors: [{key,message}] }` (mesmo formato de
  `CreateSale`; distinção com `404` fica para US2 — ver T028) (depende de T015, T016) em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 completo — `PUT /api/sales/{id}` reconcilia itens e atualiza o cabeçalho de
forma independentemente testável.

---

## Phase 4: User Story 2 - Impedir alterações que violem invariantes de negócio (Priority: P2)

**Goal**: o sistema recusa `PUT` contra venda já cancelada, corpo sem item, `id`
inexistente ou já cancelado, produto alterado em item existente, quantidade fora de 1–20 e
produto duplicado — sempre com `400` e identificação clara da condição; venda inexistente
responde `404`.

**Independent Test**: enviar cada pedido inválido (venda cancelada, corpo vazio, `id`
inexistente/cancelado, produto alterado, quantidade inválida, produto duplicado, venda
inexistente) e confirmar que nenhum é aplicado, que o estado da venda permanece intacto, e que
o status/`key` retornado corresponde à condição violada (ver `quickstart.md`, Cenários 2 e 4).

### Tests for User Story 2 ⚠️

- [X] T018 [P] [US2] Teste unitário: `Sale.Update` em venda cancelada retorna `Failure` com
  chave `"sale"`, sem mutar nada (INV-07, FR-008) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T019 [P] [US2] Teste unitário: `Sale.Update` com corpo de itens vazio retorna `Failure`
  com chave `"items"` (INV-01, FR-009) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T020 [P] [US2] Teste unitário: `Sale.Update` com `id` que não pertence à venda retorna
  `Failure` com chave `"items[{i}].id"` (FR-010) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T021 [P] [US2] Teste unitário: `Sale.Update` referenciando `id` de um item já
  cancelado (cancelado por uma chamada anterior a `Update` que o omitiu) retorna `Failure` com
  chave `"items[{i}].id"`, sem reativá-lo (FR-010, clarificação da sessão 2026-08-09) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T022 [P] [US2] Teste unitário: `Sale.Update` alterando o produto de um item existente
  (mesmo `id`, `Product.Id` diferente) retorna `Failure` com chave
  `"items[{i}].product.id"`, sem alterar o produto do item (FR-004, clarificação) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T023 [P] [US2] Teste unitário: `Sale.Update` com quantidade fora de 1–20, tanto em item
  novo quanto em item existente, retorna `Failure` com chave `"items[{i}].quantity"` (INV-02,
  FR-011) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T024 [P] [US2] Teste unitário: `Sale.Update` com o mesmo produto em dois itens do corpo
  retorna `Failure` com chave `"items[{i}].product.id"` (INV-03, FR-012) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T025 [P] [US2] Teste unitário: `Sale.Update` sem `saleDate`, sem `customer` válido ou sem
  `branch` válido retorna `Failure` com chave `"saleDate"`/`"customer"`/`"branch"`
  respectivamente (FR-002, clarificação) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T026 [P] [US2] Teste unitário: `UpdateSaleCommandHandler` com `Id` que não corresponde a
  nenhuma venda retorna `Result.Failure` com `Notification.Key == "id"`, sem persistir nada
  (depende de T015) em
  `tests/SalesApi.Application.Tests/Sales/UpdateSaleCommandHandlerTests.cs`
- [X] T027 [P] [US2] Teste de integração: `PUT /api/sales/{id}` com venda inexistente retorna
  `404` com `errors[0].key = "id"`; cada violação de negócio acima retorna `400` com a `key`
  correspondente, conforme `contracts/update-sale.md`, em
  `tests/SalesApi.Api.Tests/Sales/UpdateSaleEndpointTests.cs`

### Implementation for User Story 2

- [X] T028 [US2] Endpoint: distinguir `404` (venda não encontrada) de `400` (regra de negócio
  violada) verificando `result.Errors.Any(e => e.Key == "id")` — mesma convenção de
  `GetSaleQueryHandler`/`GetSale` (ver `research.md`, seção 2) (depende de T017) em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 e US2 funcionam de forma independente — reconciliação bem-sucedida e cada
caminho de rejeição, incluindo a distinção `404`/`400`.

---

## Phase 5: User Story 3 - Rastrear a alteração da venda por eventos de domínio (Priority: P3)

**Goal**: toda alteração bem-sucedida produz exatamente os eventos esperados (um `SaleModified`
sempre, mais um `ItemCancelled` por item removido implicitamente) e cada um é registrado em log
estruturado, permitindo auditoria sem consulta adicional.

**Independent Test**: executar uma alteração que remove implicitamente itens e outra que só
atualiza/adiciona, verificando a contagem exata de eventos emitidos em cada caso, e confirmar
que uma tentativa rejeitada não emite nenhum evento (ver `spec.md`, User Story 3, Acceptance
Scenarios 1–3).

### Tests for User Story 3 ⚠️

- [X] T029 [P] [US3] Teste unitário: `Sale.Update` que remove implicitamente dois itens registra
  exatamente 2 `ItemCancelled` (um por item, com `SaleItemId`/`ProductId`/`Quantity`
  corretos) seguidos de 1 `SaleModified` em `sale.DomainEvents` (FR-015, FR-016) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T030 [P] [US3] Teste unitário: `Sale.Update` que só atualiza/adiciona itens (nenhuma
  remoção implícita) registra exatamente 1 `SaleModified` e nenhum `ItemCancelled` (FR-016) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T031 [P] [US3] Teste unitário: `Sale.Update` rejeitado por qualquer regra de negócio não
  registra nenhum evento em `sale.DomainEvents` (FR-018) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T032 [P] [US3] Teste unitário: `SaleModifiedEventHandler` loga `SaleId`, `SaleNumber` e
  `TotalAmount` (mesmo padrão de `SaleCreatedEventHandlerTests`) em
  `tests/SalesApi.Application.Tests/Sales/Events/SaleModifiedEventHandlerTests.cs`
- [X] T033 [P] [US3] Teste unitário: `ItemCancelledEventHandler` loga `SaleId`, `SaleItemId` e
  `ProductId` em
  `tests/SalesApi.Application.Tests/Sales/Events/ItemCancelledEventHandlerTests.cs`

### Implementation for User Story 3

- [X] T034 [P] [US3] `SaleModifiedEventHandler` (`INotificationHandler<SaleModified>`): log
  estruturado (`LogInformation` com `SaleId`, `SaleNumber`, `TotalAmount`), mesmo formato de
  `SaleCreatedEventHandler` (depende de T002) em
  `src/SalesApi.Application/Sales/Events/SaleModifiedEventHandler.cs`
- [X] T035 [P] [US3] `ItemCancelledEventHandler` (`INotificationHandler<ItemCancelled>`): log
  estruturado (`LogInformation` com `SaleId`, `SaleItemId`, `ProductId`) (depende de T003) em
  `src/SalesApi.Application/Sales/Events/ItemCancelledEventHandler.cs`

**Checkpoint**: as 3 user stories funcionam de forma independente — US1 reconcilia, US2 protege
as invariantes, US3 garante rastreabilidade auditável de cada alteração.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T036 [P] Rodar os Cenários 1–4 de `quickstart.md` manualmente contra o ambiente Docker e
  confirmar cada resposta contra `contracts/update-sale.md`
- [X] T037 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX):
  `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`, sem regressão em
  `002-registrar-venda`/`003-consultar-venda`/`004-listar-vendas`
- [X] T038 Build dos 4 projetos + testes sem warnings, respeitando
  `TreatWarningsAsErrors=true` de `Directory.Build.props`
- [X] T039 [P] Adicionar sumário, descrição e `Produces<SaleResponse>(200)` /
  `Produces(400)` / `Produces(404)` às anotações do Swagger do `PUT /api/sales/{id}` em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem tasks — nada bloqueia o início da Phase 2
- **Foundational (Phase 2)**: T001–T005, sem dependência entre si exceto T005→(T001, T004) —
  BLOQUEIA todas as user stories
- **User Stories (Phase 3–5)**: todas dependem da Phase 2 completa; US2 e US3 estendem o mesmo
  `Sale.Update`/`UpdateSaleCommandHandler`/endpoint criados em US1 (T012–T017), por isso seguem
  na prática a ordem P1 → P2 → P3, ainda que independentemente testáveis a cada checkpoint
- **Polish (Phase 6)**: depende de todas as user stories desejadas estarem completas

### User Story Dependencies

- **US1 (P1)**: depende apenas da Phase 2 (Foundational)
- **US2 (P2)**: depende de US1 — a validação que ela testa já nasce dentro do two-pass de
  `Sale.Update` (T013); a única implementação nova é a distinção `404`/`400` no endpoint (T028)
- **US3 (P3)**: depende de US1 — a emissão dos eventos já nasce dentro de `Sale.Update` (T013);
  a implementação nova são os dois `INotificationHandler` de log (T034, T035)

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio
  II, TDD)
- Domain antes de Application antes de Api (Princípio V)
- História completa e com checkpoint validado antes de seguir para a próxima prioridade

### Parallel Opportunities

- T001, T002, T003 e T004 (Foundational) em paralelo entre si; T005 depende de T001 e T004
- T006, T007, T008, T009, T010 e T011 (testes de US1) em paralelo entre si
- T014 e T016 (US1: DTOs) podem ser feitos em paralelo entre si e com a escrita de T006–T011,
  antes de T013/T015/T017
- T018 a T027 (testes de US2) em paralelo entre si
- T029 a T033 (testes de US3) em paralelo entre si
- T034 e T035 (implementação de US3) em paralelo entre si
- T036, T037 e T039 (Polish) em paralelo entre si

---

## Parallel Example: User Story 1

```bash
# Testes de US1 em paralelo (todos devem falhar antes da implementação):
Task: "Teste unitário de Sale.Update (atualizar item existente) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de Sale.Update (adicionar item novo) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de Sale.Update (cancelar item implícito) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de Sale.Update (atualizar cabeçalho) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de UpdateSaleCommandHandler (fluxo principal) em tests/SalesApi.Application.Tests/Sales/UpdateSaleCommandHandlerTests.cs"
Task: "Teste de integração de PUT /api/sales/{id} (sucesso) em tests/SalesApi.Api.Tests/Sales/UpdateSaleEndpointTests.cs"

# Implementação de US1:
Task: "SaleItem.ApplyChange e SaleItem.Cancel em src/SalesApi.Domain/Sales/SaleItem.cs"
# Sale.Update (T013) depende de SaleItem.ApplyChange/Cancel (T012) — não roda em paralelo com ele
```

---

## Implementation Strategy

### MVP First (User Story 1 apenas)

1. Completar Phase 2: Foundational (`SaleItemChangeInput`, eventos, `SaleItemChangeRequest`,
   `UpdateSaleMappingConfig`)
2. Completar Phase 3: User Story 1
3. **Parar e validar**: rodar `quickstart.md` Cenário 1 manualmente; confirmar testes de US1
   passando
4. Neste ponto já existe um `PUT /api/sales/{id}` funcional, reconciliando itens e cabeçalho —
   suficiente para demonstrar o núcleo do UC-04 numa entrevista

### Incremental Delivery

1. Foundational → base pronta (value object, eventos, DTO de item, mapeamento)
2. US1 → validar isoladamente → MVP demonstrável (reconciliação completa)
3. US2 → validar isoladamente → toda tentativa inválida rejeitada com status/chave corretos
4. US3 → validar isoladamente → cada alteração auditável por log estruturado
5. Polish → cobertura, build limpo, documentação interativa

### Parallel Team Strategy

Como US2 e US3 estendem o mesmo `Sale.Update`/`UpdateSaleCommandHandler`/endpoint criados por
US1, esta feature tem pouco a ganhar com paralelismo entre desenvolvedores diferentes por user
story — o ganho real de paralelismo está dentro de cada fase, entre as tasks de teste marcadas
`[P]`, e entre os itens independentes da Phase 2.

## Notes

- [P] = arquivos diferentes (ou testes independentes no mesmo arquivo, sem dependência entre
  si), sem dependência de tasks incompletas
- [US1]/[US2]/[US3] rastreiam cada task até a user story de `spec.md`
- Rodar os testes e vê-los falhar antes de implementar (Princípio II, TDD) — em US2/US3 isso
  significa escrever o teste referenciando comportamento que `Sale.Update` (T013) já precisa
  suportar desde o primeiro commit para compilar com as duas branches de `Result` (mesmo padrão
  já observado em `004-listar-vendas/tasks.md`, US3)
- Única entidade de domínio tocada nesta feature: `Sale`/`SaleItem` — `SalesApi.Infrastructure`
  não é alterado, nenhuma migration nova (ver `research.md`, seção 6)
- Consultar `specs/005-alterar-venda/contracts/update-sale.md` para o formato exato de resposta
  e de erro antes de implementar T013, T015, T017 e T028
