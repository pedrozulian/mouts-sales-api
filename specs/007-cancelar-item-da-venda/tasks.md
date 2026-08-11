# Tasks: Cancelar Item da Venda

**Input**: Design documents from `/specs/007-cancelar-item-da-venda/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/cancel-sale-item.md](./contracts/cancel-sale-item.md),
[quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios — o Princípio II da constitution (TDD, não negociável) exige
teste que falhe antes de qualquer linha de produção nova.

**Organization**: tasks agrupadas pelas 4 user stories de `spec.md` (P1, P2, P2, P3). US1 entrega
o fluxo principal — `Sale.CancelItem()` já implementado por completo, incluindo as três
invariantes (venda cancelada, item inexistente, item já cancelado) e a cascata (INV-09), pois é um
único método coeso (ver `research.md`, seção 1). US2, US3 e US4 acrescentam cobertura de teste
dedicada às partes desse mesmo método já escritas em US1, mais o trabalho de implementação
genuinamente novo e separável de cada uma: US2 (cascata) não tem nenhuma implementação nova — o
comportamento já nasce dentro de `Sale.CancelItem()`; US3 acrescenta a tradução de
`DbUpdateConcurrencyException` no handler e a distinção `404`/`400` no endpoint; US4 não tem
nenhuma implementação nova — `ItemCancelledEventHandler` (005) e `SaleCancelledEventHandler` (006)
já existem e já processam os eventos emitidos por `Sale.CancelItem()`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tasks incompletas)
- **[Story]**: US1, US2, US3 ou US4 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes): `src/SalesApi.Domain/`,
`src/SalesApi.Application/`, `src/SalesApi.Api/`, com `tests/SalesApi.Domain.Tests/`,
`tests/SalesApi.Application.Tests/`, `tests/SalesApi.Api.Tests/` espelhando as camadas tocadas por
esta feature — conforme `plan.md`. Diferente de `006-cancelar-venda`, esta feature **não toca**
`SalesApi.Infrastructure/` — o token de concorrência `xmin` já mapeado em `SaleConfiguration`
(006) é reaproveitado sem nenhuma alteração (ver `research.md`, seção 4).

---

## Phase 1: Setup

Sem tasks nesta feature. Nenhuma dependência nova é necessária — todo o ferramental (pacotes
NuGet, `docker-compose.yml`, CI) já foi configurado em `002-registrar-venda` e é reaproveitado sem
alteração (ver `plan.md`, Technical Context).

---

## Phase 2: Foundational (Blocking Prerequisites)

Sem tasks nesta feature. Diferente de `006-cancelar-venda` (que precisou do novo evento
`SaleCancelled` antes de `Sale.Cancel()` compilar), esta feature reaproveita integralmente os dois
eventos de que precisa — `ItemCancelled` (005) e `SaleCancelled` (006) — e o método `Cancel()`
(006) que a cascata delega. Nenhum tipo compartilhado novo bloqueia o início de US1.

**Checkpoint**: nada bloqueia — as user stories podem começar imediatamente.

---

## Phase 3: User Story 1 - Cancelar um item ativo de uma venda ativa (Priority: P1) 🎯 MVP

**Goal**: `DELETE /api/sales/{id}/items/{itemId}` cancela logicamente um item ativo, mantém os
demais itens inalterados, recalcula o total geral da venda e responde `204` sem corpo.

**Independent Test**: registrar uma venda com dois ou mais itens (`POST /api/sales`, já
existente), cancelar um deles e confirmar que apenas esse item passa a constar como cancelado, que
os demais permanecem com seu estado original e que o total geral reflete apenas os itens ainda
ativos (ver `quickstart.md`, Cenário 1).

### Tests for User Story 1 ⚠️

> Escrever estes testes primeiro e vê-los falhar antes de implementar.

- [X] T001 [P] [US1] Teste unitário: `Sale.CancelItem(itemId)` em venda ativa com dois ou mais
  itens ativos marca apenas o item indicado como cancelado e recalcula `TotalAmount` somando os
  itens ainda ativos (FR-002, FR-003, INV-06) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T002 [P] [US1] Teste unitário: `Sale.CancelItem(itemId)` mantém inalterado qualquer item que
  já estivesse cancelado individualmente antes da chamada, ao cancelar um item diferente (FR-002)
  em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T003 [P] [US1] Teste unitário: `CancelSaleItemCommandHandler` com venda e item ativos
  existentes chama `Sale.CancelItem(itemId)`, persiste via `SaveChangesAsync` e retorna
  `Result.Success()`, usando `AppDbContext` com `UseInMemoryDatabase` (mesmo padrão de
  `CancelSaleCommandHandlerTests`) em
  `tests/SalesApi.Application.Tests/Sales/CancelSaleItemCommandHandlerTests.cs`
- [X] T004 [P] [US1] Teste de integração: `DELETE /api/sales/{id}/items/{itemId}` em item ativo
  entre outros itens ativos retorna `204 No Content` sem corpo; `GET /api/sales/{id}` em seguida
  confirma o item cancelado, os demais ativos e o total recalculado, conforme
  `contracts/cancel-sale-item.md`, via `WebApplicationFactory<Program>` em
  `tests/SalesApi.Api.Tests/Sales/CancelSaleItemEndpointTests.cs`

### Implementation for User Story 1

- [X] T005 [US1] `Sale.CancelItem(Guid itemId)`: implementação completa em uma única passagem —
  rejeita se `IsCancelled` (chave `"sale"`, INV-07/FR-005); localiza o item em `Items` (chave
  `"itemId"` se não encontrado, FR-006/FR-007); rejeita se o item já está cancelado (chave
  `"item"`, INV-08/FR-008); cancela o item via `SaleItem.Cancel()` já existente; recalcula
  `TotalAmount` somando `Items.Where(i => !i.IsCancelled)`; atualiza `UpdatedAt`; registra
  `ItemCancelled`; e, quando não resta nenhum item ativo, delega para `Cancel()` (já existente
  desde 006) em vez de duplicar a cascata (INV-09/FR-009, ver `research.md`, seção 1) em
  `src/SalesApi.Domain/Sales/Sale.cs`
- [X] T006 [P] [US1] `CancelSaleItemCommand` (`record SaleId, ItemId : IRequest<Result>` —
  resposta sem corpo, ver `research.md`, seção 7) em
  `src/SalesApi.Application/Sales/CancelItem/CancelSaleItemCommand.cs`
- [X] T007 [US1] `CancelSaleItemCommandHandler`: carrega a venda **com tracking**
  (`_context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == request.SaleId, ...)`,
  mesmo padrão de `CancelSaleCommandHandler` — ver `research.md`, seção 6); se não encontrada,
  `Result.Failure(new Notification("id", "Venda não encontrada."))`; senão chama
  `sale.CancelItem(request.ItemId)`; em falha, propaga os erros; em sucesso, `SaveChangesAsync` e
  retorna `Result.Success()`; log estruturado (`LogWarning` quando não encontrada,
  `LogInformation` quando cancelada com sucesso) (depende de T005, T006) em
  `src/SalesApi.Application/Sales/CancelItem/CancelSaleItemCommandHandler.cs`
- [X] T008 [US1] Endpoint `DELETE /api/sales/{id:guid}/items/{itemId:guid}`: envia
  `new CancelSaleItemCommand(id, itemId)` via `ISender`, sucesso → `Results.NoContent()`, falha →
  `Results.BadRequest` com `{ errors: [{key,message}] }` (mesmo formato dos demais endpoints;
  distinção com `404` fica para US3 — ver T019) (depende de T007) em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 completo — `DELETE /api/sales/{id}/items/{itemId}` cancela um item de forma
independentemente testável.

---

## Phase 4: User Story 2 - Cancelar o último item ativo encerra a venda (Priority: P2)

**Goal**: ao cancelar o único item ainda ativo de uma venda, o sistema também cancela a venda
inteira, na mesma operação, sem exigir uma segunda requisição.

**Independent Test**: registrar uma venda com um único item (ou cancelar previamente todos os
itens de uma venda exceto o último), cancelar esse último item ativo e confirmar que tanto o item
quanto a venda passam a constar como cancelados após uma única requisição (ver `quickstart.md`,
Cenário 2).

### Tests for User Story 2 ⚠️

- [X] T009 [P] [US2] Teste unitário: `Sale.CancelItem(itemId)` no único item ainda ativo marca o
  item como cancelado, marca a venda como cancelada (`IsCancelled = true`) e zera `TotalAmount`
  (INV-09, FR-009) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T010 [P] [US2] Teste unitário: `Sale.CancelItem(itemId)` cancela a venda mesmo quando outros
  itens já estavam cancelados individualmente antes desta chamada e o item alvo é o último ainda
  ativo (edge case de `spec.md`) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T011 [P] [US2] Teste de integração: `DELETE /api/sales/{id}/items/{itemId}` no último item
  ativo retorna `204 No Content`; `GET /api/sales/{id}` em seguida confirma a venda com
  `isCancelled: true`, todos os itens cancelados e `totalAmount: 0.00` (SC-002) em
  `tests/SalesApi.Api.Tests/Sales/CancelSaleItemEndpointTests.cs`

### Implementation for User Story 2

Nenhuma implementação nova. A cascata já está implementada dentro de `Sale.CancelItem()` (T005,
US1), que delega para `Cancel()` (006) quando não resta item ativo — ver `research.md`, seção 1.
Esta fase garante cobertura de teste dedicada ao comportamento observável (SC-002).

**Checkpoint**: US1 e US2 funcionam de forma independente — cancelamento de item isolado e
cascata para o cancelamento da venda quando aplicável.

---

## Phase 5: User Story 3 - Impedir cancelamento de item inválido (Priority: P2)

**Goal**: o sistema recusa o cancelamento quando a venda não existe (`404`), o item não existe ou
não pertence à venda (`404`), a venda já está cancelada (`400`) ou o item já está cancelado
(`400`) — e garante que, entre duas solicitações concorrentes para o mesmo item, exatamente uma
seja aplicada, a outra recebendo `400` (FR-005 a FR-008, FR-013 a FR-015).

**Independent Test**: solicitar o cancelamento contra uma venda inexistente, um item inexistente,
uma venda já cancelada e um item já cancelado, confirmando o status e a chave de erro corretos em
cada caso; disparar duas requisições concorrentes contra o mesmo item ativo e confirmar exatamente
uma `204` e uma `400` (ver `quickstart.md`, Cenários 3 a 7).

### Tests for User Story 3 ⚠️

- [X] T012 [P] [US3] Teste unitário: `Sale.CancelItem(itemId)` em venda já cancelada retorna
  `Failure` com chave `"sale"`, sem localizar o item nem mutar nada (INV-07, FR-005) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T013 [P] [US3] Teste unitário: `Sale.CancelItem(itemId)` com identificador que não
  corresponde a nenhum item da venda retorna `Failure` com chave `"itemId"` (FR-006, FR-007) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T014 [P] [US3] Teste unitário: `Sale.CancelItem(itemId)` em item já cancelado retorna
  `Failure` com chave `"item"`, sem alterar `TotalAmount` nem registrar nenhum evento (INV-08,
  FR-008) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T015 [P] [US3] Teste unitário: `CancelSaleItemCommandHandler` com `SaleId` que não
  corresponde a nenhuma venda retorna `Result.Failure` com `Notification.Key == "id"`, sem
  persistir nada (depende de T007) em
  `tests/SalesApi.Application.Tests/Sales/CancelSaleItemCommandHandlerTests.cs`
- [X] T016 [P] [US3] Teste de integração: `DELETE /api/sales/{id}/items/{itemId}` em venda
  inexistente retorna `404` com `errors[0].key = "id"`; em item inexistente ou de outra venda
  retorna `404` com `errors[0].key = "itemId"`; em venda já cancelada retorna `400` com
  `errors[0].key = "sale"`; em item já cancelado retorna `400` com `errors[0].key = "item"`,
  conforme `contracts/cancel-sale-item.md`, em
  `tests/SalesApi.Api.Tests/Sales/CancelSaleItemEndpointTests.cs`
- [X] T017 [P] [US3] Teste de integração: duas requisições `DELETE /api/sales/{id}/items/{itemId}`
  concorrentes para o mesmo item ativo via `Task.WhenAll` (mesmo padrão de
  `CancelSaleConcurrencyTests.cs`, 006) resultam em exatamente uma resposta `204` e uma `400` com
  `errors[0].key = "item"`, nunca duas `204` nem um `500` (FR-015) em
  `tests/SalesApi.Api.Tests/Sales/CancelSaleItemConcurrencyTests.cs`

### Implementation for User Story 3

- [X] T018 [US3] `CancelSaleItemCommandHandler`: envolve `SaveChangesAsync` em `try/catch
  (DbUpdateConcurrencyException)`, traduzindo para `Result.Failure(new Notification("item", "Item
  já está cancelado."))` — reaproveita o token `xmin` já mapeado por `SaleConfiguration` (006),
  nenhuma mudança de Infrastructure (ver `research.md`, seção 4) (depende de T007) em
  `src/SalesApi.Application/Sales/CancelItem/CancelSaleItemCommandHandler.cs`
- [X] T019 [US3] Endpoint: distinguir `404` (venda ou item não encontrados) de `400` (venda já
  cancelada, item já cancelado ou conflito de concorrência) verificando `result.Errors.Any(e =>
  e.Key == "id" || e.Key == "itemId")` — mesma convenção de `UpdateSale`/`CancelSale`, estendida
  com a nova chave `itemId` (ver `research.md`, seção 3) (depende de T008) em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1, US2 e US3 funcionam de forma independente — cancelamento bem-sucedido,
cascata, cada caminho de rejeição e a garantia de concorrência (FR-015).

---

## Phase 6: User Story 4 - Rastrear o cancelamento do item por evento de domínio (Priority: P3)

**Goal**: todo cancelamento de item bem-sucedido produz exatamente um evento `ItemCancelled`; um
`SaleCancelled` adicional só é emitido quando o cancelamento também encerra a venda (cascata),
ambos na mesma operação; nenhum evento é emitido quando a solicitação é rejeitada.

**Independent Test**: cancelar um item que não é o último ativo e verificar que exatamente um
`ItemCancelled` é emitido; cancelar o último item ativo e verificar que `ItemCancelled` e
`SaleCancelled` são emitidos juntos; confirmar que uma tentativa rejeitada não emite nenhum evento
(ver `spec.md`, User Story 4, Acceptance Scenarios 1–3).

### Tests for User Story 4 ⚠️

- [X] T020 [P] [US4] Teste unitário: `Sale.CancelItem(itemId)` bem-sucedido sem esgotar os itens
  ativos registra exatamente 1 `ItemCancelled` em `sale.DomainEvents`, nenhum `SaleCancelled`
  (FR-011) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T021 [P] [US4] Teste unitário: `Sale.CancelItem(itemId)` bem-sucedido no último item ativo
  registra 1 `ItemCancelled` e 1 `SaleCancelled` em `sale.DomainEvents`, ambos na mesma chamada
  (FR-012) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T022 [P] [US4] Teste unitário: `Sale.CancelItem(itemId)` rejeitado, por qualquer uma das
  condições de US3, não registra nenhum evento em `sale.DomainEvents` (FR-013) em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`

### Implementation for User Story 4

Nenhuma implementação nova. `ItemCancelledEventHandler` (005) e `SaleCancelledEventHandler` (006)
já existem e já processam os eventos emitidos por `Sale.CancelItem()` (T005, US1) sem qualquer
alteração — ver `research.md`, seção 5. Esta fase garante cobertura de teste dedicada à
contabilidade exata dos eventos.

**Checkpoint**: as 4 user stories funcionam de forma independente — US1 cancela o item, US2
garante a cascata, US3 protege as invariantes e a concorrência, US4 garante rastreabilidade
auditável de cada cancelamento.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T023 [P] Rodar os Cenários 1–7 de `quickstart.md` manualmente contra o ambiente Docker e
  confirmar cada resposta contra `contracts/cancel-sale-item.md` — validado de forma equivalente
  pela suíte `SalesApi.Api.Tests` (Postgres real), que cobre os 7 cenários: cancelamento sem
  cascata (1), cascata no último item (2), venda inexistente (3), item inexistente (4), venda já
  cancelada (5), item já cancelado (6) e concorrência (7, `CancelSaleItemConcurrencyTests`)
- [X] T024 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX):
  `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`, sem regressão nas features
  002–006; todo o código novo desta feature (`Sale.CancelItem()`, `CancelSaleItemCommand`/
  `Handler`, ajuste no endpoint) em 100% de cobertura de linha, incluindo o branch de
  `DbUpdateConcurrencyException` (T018) — se o teste de integração concorrente (T017) não
  exercitar esse branch de forma determinística por depender de timing real entre duas
  requisições HTTP, seguir a mesma técnica já usada em `CancelSaleCommandHandlerTests.cs` (006):
  forçar `OriginalValue` do shadow property `xmin` via `context.Entry(...).Property("xmin")
  .OriginalValue` no provider InMemory
- [X] T025 Build dos 4 projetos + testes sem warnings, respeitando `TreatWarningsAsErrors=true` de
  `Directory.Build.props` — `dotnet build SalesApi.sln -warnaserror` limpo (0 Warning(s), 0
  Error(s))
- [X] T026 [P] Adicionar sumário, descrição e `Produces(204)` / `Produces(400)` / `Produces(404)`
  às anotações do Swagger do `DELETE /api/sales/{id:guid}/items/{itemId:guid}` em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem tasks — nada bloqueia o início da Phase 3
- **Foundational (Phase 2)**: sem tasks — nada bloqueia o início da Phase 3
- **User Stories (Phase 3–6)**: US2, US3 e US4 estendem o mesmo `Sale.CancelItem()`/
  `CancelSaleItemCommandHandler`/endpoint criados em US1 (T005–T008), por isso seguem na prática a
  ordem P1 → P2 → P2 → P3, ainda que independentemente testáveis a cada checkpoint
- **Polish (Phase 7)**: depende de todas as user stories desejadas estarem completas

### User Story Dependencies

- **US1 (P1)**: sem dependência de outra user story
- **US2 (P2)**: depende de US1 — a cascata já nasce dentro de `Sale.CancelItem()` (T005); o
  trabalho novo é exclusivamente de teste (T009–T011)
- **US3 (P2)**: depende de US1 — as três invariantes já nascem dentro de `Sale.CancelItem()`
  (T005); o trabalho genuinamente novo é a tradução da exceção de concorrência (T018) e a
  distinção `404`/`400` no endpoint (T019)
- **US4 (P3)**: depende de US1 e, para o cenário de cascata (T021), também de US2 — a emissão dos
  eventos já nasce dentro de `Sale.CancelItem()` (T005); não há implementação nova, apenas testes

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio II,
  TDD)
- Domain antes de Application antes de Api (Princípio V)
- História completa e com checkpoint validado antes de seguir para a próxima prioridade

### Parallel Opportunities

- T001 a T004 (testes de US1) em paralelo entre si
- T006 (US1: `CancelSaleItemCommand`) pode ser feito em paralelo com a escrita de T001–T004, antes
  de T005/T007/T008
- T009 a T011 (testes de US2) em paralelo entre si
- T012 a T017 (testes de US3) em paralelo entre si
- T020 a T022 (testes de US4) em paralelo entre si
- T023, T024 e T026 (Polish) em paralelo entre si

---

## Parallel Example: User Story 1

```bash
# Testes de US1 em paralelo (todos devem falhar antes da implementação):
Task: "Teste unitário de Sale.CancelItem() (item entre outros ativos) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de Sale.CancelItem() (itens já cancelados individualmente permanecem intocados) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de CancelSaleItemCommandHandler (fluxo principal) em tests/SalesApi.Application.Tests/Sales/CancelSaleItemCommandHandlerTests.cs"
Task: "Teste de integração de DELETE /api/sales/{id}/items/{itemId} (sucesso, sem cascata) em tests/SalesApi.Api.Tests/Sales/CancelSaleItemEndpointTests.cs"

# Implementação de US1:
Task: "CancelSaleItemCommand em src/SalesApi.Application/Sales/CancelItem/CancelSaleItemCommand.cs"
# Sale.CancelItem() (T005) não depende de nenhum tipo novo — ItemCancelled (005) e Cancel() (006) já existem
# CancelSaleItemCommandHandler (T007) depende de Sale.CancelItem() (T005) e CancelSaleItemCommand (T006)
```

---

## Implementation Strategy

### MVP First (User Story 1 apenas)

1. Completar Phase 3: User Story 1
2. **Parar e validar**: rodar `quickstart.md` Cenário 1 manualmente; confirmar testes de US1
   passando
3. Neste ponto já existe um `DELETE /api/sales/{id}/items/{itemId}` funcional, cancelando um item
   individual sem afetar os demais — suficiente para demonstrar o núcleo do UC-06 numa entrevista

### Incremental Delivery

1. US1 → validar isoladamente → MVP demonstrável (cancelamento de item isolado)
2. US2 → validar isoladamente → cascata para cancelamento da venda quando o último item ativo é
   cancelado
3. US3 → validar isoladamente → toda tentativa inválida rejeitada com status/chave corretos,
   inclusive sob concorrência
4. US4 → validar isoladamente → cada cancelamento (com ou sem cascata) auditável por log
   estruturado, via os event handlers já existentes
5. Polish → cobertura, build limpo, documentação interativa

### Parallel Team Strategy

Como US2, US3 e US4 estendem o mesmo `Sale.CancelItem()`/`CancelSaleItemCommandHandler`/endpoint
criados por US1, esta feature tem pouco a ganhar com paralelismo entre desenvolvedores diferentes
por user story — o ganho real de paralelismo está dentro de cada fase, entre as tasks de teste
marcadas `[P]`.

## Notes

- [P] = arquivos diferentes (ou testes independentes no mesmo arquivo, sem dependência entre si),
  sem dependência de tasks incompletas
- [US1]/[US2]/[US3]/[US4] rastreiam cada task até a user story de `spec.md`
- Rodar os testes e vê-los falhar antes de implementar (Princípio II, TDD)
- T005 já implementa as três invariantes de rejeição (venda cancelada, item inexistente, item já
  cancelado) e a cascata, mesmo que os testes dedicados a cada uma dessas partes só apareçam em
  US2/US3/US4 — mesmo padrão já validado em `006-cancelar-venda/tasks.md` (`Sale.Cancel()`, T006,
  já continha a invariante testada depois por T010)
- Consultar `specs/007-cancelar-item-da-venda/contracts/cancel-sale-item.md` para o formato exato
  de resposta e de erro antes de implementar T005, T007, T008 e T019
- Esta feature não toca `SalesApi.Infrastructure/` — diferença notável em relação a
  `006-cancelar-venda`, que precisou mapear o token `xmin`. Aqui ele já existe e é apenas
  reaproveitado (T018)
- Se, durante T024, o branch de `catch (DbUpdateConcurrencyException)` de
  `CancelSaleItemCommandHandler` (T018) não for exercitado de forma determinística pelo teste de
  integração concorrente (T017) — mesmo problema já observado em `006-cancelar-venda` —, adicionar
  um teste unitário determinístico extra em `CancelSaleItemCommandHandlerTests.cs`, forçando o
  `OriginalValue` do shadow property `xmin` (mesma técnica documentada em
  `specs/006-cancelar-venda/tasks.md`, Notes)
