# Tasks: Registrar Venda

**Input**: Design documents from `/specs/002-registrar-venda/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/create-sale.md](./contracts/create-sale.md),
[quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios — o Princípio II da constitution (TDD, não negociável)
exige teste que falhe antes de qualquer linha de produção. Toda task de teste MUST ser
concluída, rodada e vista falhando antes da task de implementação correspondente.

**Organization**: tasks agrupadas pelas 3 user stories de `spec.md` (P1, P2, P3). As três
compartilham o mesmo endpoint (`POST /api/sales`), então a implementação de US1 entrega o
esqueleto completo; US2 e US3 endurecem esse mesmo código com validação e rastreabilidade,
cada uma com seus próprios testes e checkpoint de verificação independente.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tasks incompletas)
- **[Story]**: US1, US2 ou US3 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes): `src/SalesApi.Domain/`,
`src/SalesApi.Application/`, `src/SalesApi.Infrastructure/`, `src/SalesApi.Api/`, com
`tests/SalesApi.Domain.Tests/`, `tests/SalesApi.Application.Tests/`,
`tests/SalesApi.Api.Tests/` espelhando cada camada testável — conforme `plan.md`.

---

## Phase 1: Setup

**Purpose**: dependências de ferramentas necessárias antes de qualquer código desta feature.

- [X] T001 [P] Adicionar o pacote `Microsoft.EntityFrameworkCore.Design` a
  `src/SalesApi.Api/SalesApi.Api.csproj` (ferramenta necessária para `dotnet ef migrations
  add`, ainda não referenciada no repositório)
- [X] T002 [P] Adicionar o pacote `Microsoft.EntityFrameworkCore.InMemory` a
  `tests/SalesApi.Application.Tests/SalesApi.Application.Tests.csproj` (permite testar o
  `CreateSaleCommandHandler` com um `AppDbContext` real em memória, sem depender do
  PostgreSQL local)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: infraestrutura de domínio compartilhada pelas 3 user stories.

**⚠️ CRITICAL**: nenhuma user story começa antes desta fase estar completa.

### Tests for Foundational ⚠️

> Escrever este teste primeiro e vê-lo falhar antes de implementar (Princípio II, TDD não
> negociável — aplica-se também ao código de infraestrutura de domínio, não só às user
> stories).

- [X] T003 [P] Teste unitário de `AddDomainEvent`/`ClearDomainEvents` em `Entity`: adicionar
  um evento popula `DomainEvents`; limpar esvazia a coleção em
  `tests/SalesApi.Domain.Tests/Common/EntityTests.cs`

### Implementation for Foundational

- [X] T004 [P] Adicionar suporte a eventos de domínio em `Entity` — coleção protegida de
  `DomainEvent`, método `AddDomainEvent`, propriedade somente leitura `DomainEvents` e método
  `ClearDomainEvents` — (depende de T003) em `src/SalesApi.Domain/Common/Entity.cs`
- [X] T005 [P] Criar o value object `ExternalReference` (record `Id` + `Name`, usado por
  Customer/Branch/Product) em `src/SalesApi.Domain/Sales/ExternalReference.cs`

**Checkpoint**: fundação pronta — as user stories podem começar.

---

## Phase 3: User Story 1 - Registrar venda com desconto calculado automaticamente (Priority: P1) 🎯 MVP

**Goal**: registrar uma venda válida com um ou mais itens e devolver a venda completa, com
desconto e totais calculados automaticamente pela faixa de quantidade de cada item.

**Independent Test**: enviar uma venda válida com um item cuja quantidade caia em cada faixa
de desconto (2, 5 e 15 unidades) e verificar que o desconto, o total do item e o total da
venda retornados batem com o esperado (ver `quickstart.md`, Cenários 1–3).

### Tests for User Story 1 ⚠️

> Escrever estes testes primeiro e vê-los falhar antes de implementar.

- [X] T006 [P] [US1] Testes unitários de `DiscountPolicy` cobrindo as faixas 0%/10%/20% e as
  fronteiras 3, 4, 9, 10 e 20 unidades em
  `tests/SalesApi.Domain.Tests/Sales/DiscountPolicyTests.cs`
- [X] T007 [P] [US1] Testes unitários de `Sale.Create` para os cenários de sucesso: um item,
  múltiplos itens de produtos diferentes (total da venda = soma dos totais), e `SaleDate`
  assumindo o momento do registro quando omitida em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T008 [P] [US1] Teste unitário do `CreateSaleCommandHandler` no fluxo de sucesso: gera o
  `SaleNumber`, persiste a venda e retorna a resposta completa (com desconto e total de cada
  item) em `tests/SalesApi.Application.Tests/Sales/CreateSaleCommandHandlerTests.cs`
- [X] T009 [P] [US1] Testes de integração de `POST /api/sales` para os cenários de sucesso do
  `quickstart.md` (Cenários 1–3: 2, 5 e 15 unidades), verificando status `201`, header
  `Location` e o corpo da resposta, via `WebApplicationFactory<Program>`; incluir um caso que
  envie `discountPercentage`/`totalAmount` arbitrários no payload e confirme que a resposta
  ignora esses valores e calcula os corretos (FR-010), em
  `tests/SalesApi.Api.Tests/Sales/CreateSaleEndpointTests.cs`

### Implementation for User Story 1

- [X] T010 [P] [US1] `DiscountPolicy` (faixas 1–3/4–9/10–20) em
  `src/SalesApi.Domain/Sales/DiscountPolicy.cs`
- [X] T011 [US1] Entidade `SaleItem`, calculando `DiscountAmount` e `TotalAmount` a partir de
  `Quantity`, `UnitPrice` e `DiscountPolicy` (FR-008) (depende de T005, T010) em
  `src/SalesApi.Domain/Sales/SaleItem.cs`
- [X] T012 [US1] Agregado `Sale` com factory `Create` cobrindo o caminho feliz, calculando
  `TotalAmount` como a soma dos totais dos itens (FR-008) (depende de T011) em
  `src/SalesApi.Domain/Sales/Sale.cs`
- [X] T013 [P] [US1] DTOs de requisição/resposta (`ExternalReferenceRequest`,
  `ExternalReferenceResponse`, `SaleItemRequest`, `SaleItemResponse`, `CreateSaleRequest`,
  `SaleResponse`) em `src/SalesApi.Application/Sales/Dtos/`
- [X] T014 [US1] `CreateSaleCommand` e `CreateSaleMappingConfig` (Mapster, depende de T013) em
  `src/SalesApi.Application/Sales/Create/CreateSaleCommand.cs` e
  `src/SalesApi.Application/Sales/Create/CreateSaleMappingConfig.cs`
- [X] T015 [US1] Adicionar `DbSet<Sale> Sales` a `IApplicationDbContext` e `AppDbContext`
  (depende de T012) em `src/SalesApi.Application/Common/IApplicationDbContext.cs` e
  `src/SalesApi.Infrastructure/Persistence/AppDbContext.cs`
- [X] T016 [US1] `CreateSaleCommandHandler`: obtém o próximo valor de `sale_number_seq`,
  formata o `SaleNumber`, chama `Sale.Create`, persiste via `IApplicationDbContext` e mapeia a
  resposta (depende de T012, T014, T015) em
  `src/SalesApi.Application/Sales/Create/CreateSaleCommandHandler.cs`
- [X] T017 [P] [US1] `SaleConfiguration` e `SaleItemConfiguration` — EF Core Fluent API,
  `ExternalReference` como owned type, índice único em `SaleNumber`, unique constraint
  `sale_id`+`product_id` (INV-03) — em
  `src/SalesApi.Infrastructure/Persistence/Configurations/SaleConfiguration.cs` e
  `src/SalesApi.Infrastructure/Persistence/Configurations/SaleItemConfiguration.cs`
- [X] T018 [US1] Gerar a migration `CreateSales` incluindo a sequence `sale_number_seq`
  (`dotnet ef migrations add CreateSales --project src/SalesApi.Infrastructure --startup-project src/SalesApi.Api`,
  depende de T001, T015, T017) em `src/SalesApi.Infrastructure/Persistence/Migrations/`
- [X] T019 [US1] Endpoint `POST /api/sales` retornando `201 Created` + header `Location` no
  sucesso (depende de T016) em `src/SalesApi.Api/Sales/SalesEndpoints.cs`
- [X] T020 [US1] Registrar `app.MapSalesEndpoints()` em `src/SalesApi.Api/Program.cs` (depende
  de T019)

**Checkpoint**: US1 completo — `POST /api/sales` registra vendas válidas com desconto e
totais corretos, de forma independentemente testável.

---

## Phase 4: User Story 2 - Impedir vendas que violem as regras de quantidade e de negócio (Priority: P2)

**Goal**: recusar o registro quando qualquer item ultrapassa o limite de unidades, o mesmo
produto se repete, não há itens, ou um preço unitário é inválido — informando qual regra foi
violada.

**Independent Test**: enviar payloads inválidos (quantidade 21, produto duplicado, venda sem
itens, preço zero) e confirmar que nenhum é registrado e que a resposta identifica a regra
violada (ver `quickstart.md`, Cenários 4–5).

### Tests for User Story 2 ⚠️

- [X] T021 [P] [US2] Testes unitários de violação em `Sale.Create`/`SaleItem.Create`: sem
  itens (INV-01), quantidade 21 (INV-02), produto duplicado entre itens (INV-03), preço
  unitário ≤ 0 (INV-04) e identidade externa (cliente/filial/produto) com `Id`/`Name` vazios
  (FR-007) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T022 [P] [US2] Teste unitário do `CreateSaleCommandHandler`: quando `Sale.Create` falha,
  o handler retorna `Result` de falha e `SaveChangesAsync` nunca é chamado (FR-014) em
  `tests/SalesApi.Application.Tests/Sales/CreateSaleCommandHandlerTests.cs`
- [X] T023 [P] [US2] Testes de integração de `POST /api/sales` para os cenários de rejeição do
  `quickstart.md` (Cenários 4–5, mais venda sem itens e preço inválido), verificando status
  `400`, o corpo `{ errors: [{key,message}] }` conforme `contracts/create-sale.md`, e que
  nenhuma linha foi inserida em `sales` em
  `tests/SalesApi.Api.Tests/Sales/CreateSaleEndpointTests.cs`

### Implementation for User Story 2

- [X] T024 [US2] Implementar as validações INV-01 a INV-05 e FR-007 dentro de
  `Sale.Create`/`SaleItem.Create`, retornando `Result` com uma `Notification` por regra
  violada, com a chave conforme a tabela de `contracts/create-sale.md` (depende de T012, T011)
  em `src/SalesApi.Domain/Sales/Sale.cs` e `src/SalesApi.Domain/Sales/SaleItem.cs`
- [X] T025 [US2] Traduzir o `Result` de falha do handler para `400 Bad Request` com o corpo
  `{ errors: [{key,message}] }` no endpoint (depende de T019, T024) em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`

**Checkpoint**: US1 e US2 funcionam de forma independente — vendas válidas são registradas,
vendas inválidas são recusadas com a regra violada identificada.

---

## Phase 5: User Story 3 - Rastrear a criação da venda por evento de domínio (Priority: P3)

**Goal**: emitir um evento de criação rastreável (log estruturado) sempre que uma venda for
registrada com sucesso, sem exigir consulta adicional à API.

**Independent Test**: registrar uma venda válida e verificar que um evento `SaleCreated`
correspondente foi emitido com o identificador e o número da venda (ver `quickstart.md`,
Cenário 6).

### Tests for User Story 3 ⚠️

- [X] T026 [P] [US3] Testes unitários: `Sale.Create` bem-sucedido registra um `SaleCreated` em
  `DomainEvents`; `Sale.Create` que falha não registra nenhum evento em
  `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T027 [P] [US3] Teste de integração: `POST /api/sales` bem-sucedido produz uma entrada de
  log correspondente ao evento `SaleCreated`, contendo `SaleId` e `SaleNumber` em
  `tests/SalesApi.Api.Tests/Sales/CreateSaleEndpointTests.cs`

### Implementation for User Story 3

- [X] T028 [P] [US3] Evento de domínio `SaleCreated` (`SaleId`, `SaleNumber`, `CustomerId`,
  `BranchId`, `TotalAmount`, `OccurredOn` herdado de `DomainEvent`) em
  `src/SalesApi.Domain/Sales/Events/SaleCreated.cs`
- [X] T029 [US3] `Sale.Create` bem-sucedido chama `AddDomainEvent(new SaleCreated(...))`
  (depende de T004, T028, T012) em `src/SalesApi.Domain/Sales/Sale.cs`
- [X] T030 [P] [US3] Sobrescrever `AppDbContext.SaveChangesAsync`: após `base.SaveChangesAsync`
  bem-sucedido, coletar os `DomainEvents` das entidades rastreadas, publicá-los via
  `IPublisher` (MediatR) e limpá-los em seguida (depende de T004, T015) em
  `src/SalesApi.Infrastructure/Persistence/AppDbContext.cs`
- [X] T031 [P] [US3] `SaleCreatedEventHandler` (`INotificationHandler<SaleCreated>`)
  registrando log estruturado com `SaleId` e `SaleNumber` (depende de T028) em
  `src/SalesApi.Application/Sales/Events/SaleCreatedEventHandler.cs`

**Checkpoint**: as 3 user stories funcionam de forma independente — US1 registra e calcula,
US2 recusa o inválido, US3 torna a criação rastreável via log.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T032 [P] Rodar os 6 cenários de `quickstart.md` manualmente contra o ambiente Docker e
  confirmar cada resposta contra `contracts/create-sale.md`
- [X] T033 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX):
  `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`
- [X] T034 Build dos 4 projetos + testes sem warnings, respeitando
  `TreatWarningsAsErrors=true` de `Directory.Build.props`
- [X] T035 [P] Adicionar sumário/exemplo de `POST /api/sales` às anotações do Swagger em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs`
- [X] T036 [P] Teste de integração de concorrência: disparar múltiplas requisições
  `POST /api/sales` em paralelo e confirmar que todos os `saleNumber` retornados são distintos
  (SC-003) em `tests/SalesApi.Api.Tests/Sales/CreateSaleConcurrencyTests.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências — pode começar imediatamente
- **Foundational (Phase 2)**: depende do Setup — bloqueia as 3 user stories
- **User Stories (Phase 3–5)**: todas dependem do Foundational; entre si, US2 e US3 editam os
  mesmos arquivos de domínio que US1 criou (`Sale.cs`, `SaleItem.cs`), então na prática devem
  seguir a ordem de prioridade P1 → P2 → P3 nesta feature, mesmo sendo independentemente
  testáveis a cada checkpoint
- **Polish (Phase 6)**: depende de todas as user stories desejadas estarem completas

### User Story Dependencies

- **US1 (P1)**: depende apenas do Foundational
- **US2 (P2)**: depende do Foundational; estende o mesmo `Sale.Create`/`SaleItem.Create`
  criado em US1 (endurece a validação já existente) — por isso segue US1 nesta feature, ainda
  que testável de forma independente a partir do seu próprio checkpoint
- **US3 (P3)**: depende do Foundational (T004); estende `Sale.Create` (US1/US2) para também
  registrar o evento — segue US1/US2 pelo mesmo motivo

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio
  II, TDD)
- Domain antes de Application antes de Infrastructure antes de Api (Princípio V)
- História completa e com checkpoint validado antes de seguir para a próxima prioridade

### Parallel Opportunities

- T001 e T002 (Setup) em paralelo
- T003 (teste do Foundational) roda sozinho e precede T004; T004 e T005 (Foundational,
  implementação) em paralelo entre si depois de T003 falhar como esperado
- T006–T009 (testes de US1) em paralelo entre si
- T010 e T013 (US1: `DiscountPolicy` e DTOs) em paralelo; T017 (configurações EF) em paralelo
  com T013/T014 por estar em outra camada
- T021–T023 (testes de US2) em paralelo entre si
- T026–T027 (testes de US3) em paralelo entre si; T028, T030, T031 (impl. de US3) em paralelo
  entre si, uma vez que T028 esteja concluída

---

## Parallel Example: User Story 1

```bash
# Testes de US1 em paralelo (todos devem falhar antes da implementação):
Task: "Testes unitários de DiscountPolicy em tests/SalesApi.Domain.Tests/Sales/DiscountPolicyTests.cs"
Task: "Testes unitários de Sale.Create (sucesso) em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Teste unitário de CreateSaleCommandHandler (sucesso) em tests/SalesApi.Application.Tests/Sales/CreateSaleCommandHandlerTests.cs"
Task: "Testes de integração de POST /api/sales (sucesso) em tests/SalesApi.Api.Tests/Sales/CreateSaleEndpointTests.cs"

# Implementação de US1 que pode ser paralela:
Task: "DiscountPolicy em src/SalesApi.Domain/Sales/DiscountPolicy.cs"
Task: "DTOs de requisição/resposta em src/SalesApi.Application/Sales/Dtos/"
```

---

## Implementation Strategy

### MVP First (User Story 1 apenas)

1. Completar Phase 1 (Setup) e Phase 2 (Foundational)
2. Completar Phase 3 (US1)
3. **Parar e validar**: rodar `quickstart.md` Cenários 1–3 manualmente; confirmar testes de
   US1 passando
4. Neste ponto já existe uma API que registra vendas com desconto correto — suficiente para
   demonstrar o núcleo do caso de uso (UC-01) numa entrevista

### Incremental Delivery

1. Setup + Foundational → base pronta
2. US1 → validar isoladamente → MVP demonstrável
3. US2 → validar isoladamente → API robusta contra entrada inválida
4. US3 → validar isoladamente → criação rastreável via log
5. Polish → cobertura, build limpo, documentação interativa, concorrência do `SaleNumber`

### Parallel Team Strategy

Com múltiplos desenvolvedores: completar Setup + Foundational juntos; depois disso, os
checkpoints de US1/US2/US3 podem ser validados por pessoas diferentes, ainda que — nesta
feature específica — a implementação em si seja sequencial por editar os mesmos arquivos de
domínio (ver nota em "Phase Dependencies").

## Notes

- [P] = arquivos diferentes, sem dependência de tasks incompletas
- [US1]/[US2]/[US3] rastreiam cada task até a user story de `spec.md`
- Rodar os testes e vê-los falhar antes de implementar (Princípio II, não negociável) — vale
  também para o Foundational (T003), não só para as user stories
- Cada regra de negócio (INV-01 a INV-05, INV-10) vive em `Sale`/`SaleItem` (Domain) — nunca
  no handler ou no endpoint (Princípios I e V)
- Consultar `specs/002-registrar-venda/contracts/create-sale.md` para o formato exato de
  requisição/resposta antes de implementar T013, T019 e T025
