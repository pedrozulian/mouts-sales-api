# Tasks: Confiabilidade Operacional e Consistência de Dados

**Input**: Design documents from `/specs/008-confiabilidade-e-consistencia/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/error-contract.md](./contracts/error-contract.md),
[contracts/health-check.md](./contracts/health-check.md), [quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios onde há código de produção testável — o Princípio II da
constitution (TDD, não negociável) exige teste que falhe antes de qualquer linha de produção
nova. Tarefas de infraestrutura pura (Docker, `docker-compose.yml`, README) não têm teste
unitário dedicado — são validadas por checkpoints manuais equivalentes ao `quickstart.md`, mesmo
padrão já adotado em `001-project-setup/tasks.md`.

**Organization**: tasks agrupadas pelas 7 user stories de `spec.md` (P1, P1, P2, P2, P2, P3, P3).
Diferente de todas as features anteriores — que adicionavam um endpoint novo por vez —, esta
feature corrige sete defeitos independentes sobre a base já existente; por isso as user stories
aqui não têm relação de dependência funcional entre si (nenhuma story espera outra terminar para
ser *implementada*), com uma única exceção documentada: US5 (README) descreve o fluxo de
provisionamento de US1, então seu conteúdo final depende de US1 estar concluída.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, ou testes independentes no mesmo arquivo
  sem dependência entre si)
- **[Story]**: US1 a US7 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes). Diferente de todas as features
anteriores, esta feature toca as quatro camadas **simultaneamente**, mais `docker/` e
`README.md` (ver `plan.md`, Project Structure). Três arquivos são tocados por mais de uma user
story, sempre em métodos ou blocos distintos, sem conflito funcional:

- `src/SalesApi.Api/Program.cs` — US1 (registro do health check), US3 (registro do handler global
  de exceção), US7 (remoção de `UseHttpsRedirection`)
- `src/SalesApi.Domain/Sales/SaleItem.cs` — US2 (arredondamento), US7 (extração de
  `ValidateChange`)
- `src/SalesApi.Domain/Sales/Sale.cs` — US4 (`ReconcileNewItem`), US7 (`ReconcileExistingItem`)

---

## Phase 1: Setup

Sem tasks nesta feature. Nenhuma dependência nova é adicionada a nenhum `.csproj` — todo o
ferramental necessário (EF Core Design, Testcontainers, xUnit) já está configurado pelas features
anteriores (ver `plan.md`, Technical Context).

---

## Phase 2: Foundational (Blocking Prerequisites)

Sem tasks nesta feature. As sete user stories corrigem defeitos independentes sobre código já
existente — nenhum tipo compartilhado novo precisa existir antes que qualquer uma delas comece
(ver `Path Conventions` acima para os três arquivos tocados por mais de uma story, sem relação de
bloqueio entre elas).

**Checkpoint**: nada bloqueia — as user stories podem começar imediatamente, exceto a ressalva de
conteúdo (não de implementação) de US5 sobre US1.

---

## Phase 3: User Story 1 - Ambiente provisionado e pronto para uso (Priority: P1) 🎯 MVP

**Goal**: `docker compose up -d` a partir de um volume limpo prepara o schema como etapa própria,
concluída antes de a `api` aceitar requisições; `POST /api/sales` funciona na primeira tentativa;
`/health` reporta `Unhealthy` quando o schema está ausente ou desatualizado, mesmo com o banco
acessível.

**Independent Test**: `docker compose down -v && docker compose up -d` seguido de `POST
/api/sales` sem nenhum passo manual intermediário (ver `quickstart.md`, Cenário 1); e, contra um
banco propositalmente não migrado, `GET /health` responde `503` em vez do `200 Healthy` atual
(Cenário 2).

### Tests for User Story 1 ⚠️

- [X] T001 [P] [US1] Teste: `PendingMigrationsHealthCheck.CheckHealthAsync` retorna
  `HealthCheckResult.Unhealthy` quando `Database.GetPendingMigrationsAsync()` não é vazio, usando
  um `PostgreSqlContainer` (Testcontainers) iniciado **sem** chamar `MigrateAsync` — NOVO em
  `tests/SalesApi.Api.Tests/Infrastructure/PendingMigrationsHealthCheckTests.cs` (FR-006)
- [X] T002 [P] [US1] Teste: `PendingMigrationsHealthCheck.CheckHealthAsync` retorna
  `HealthCheckResult.Healthy` quando o mesmo container, após `MigrateAsync`, não tem nenhuma
  migration pendente — mesmo arquivo de T001 (FR-007; a suíte `HealthCheckTests.cs` existente
  continua como regressão do contrato de resposta ponta a ponta)

### Implementation for User Story 1

- [X] T003 [US1] `PendingMigrationsHealthCheck : IHealthCheck` — `CheckHealthAsync` chama
  `Database.GetPendingMigrationsAsync(cancellationToken)`, retorna `Unhealthy` com a lista de
  migrations pendentes na descrição quando não vazia, `Healthy` quando vazia — NOVO em
  `src/SalesApi.Infrastructure/HealthChecks/PendingMigrationsHealthCheck.cs` (depende de T001,
  T002 falhando antes)
- [X] T004 [US1] `Program.cs`: substituir `.AddNpgSql(...)` por
  `.AddCheck<PendingMigrationsHealthCheck>("postgresql")` — mesmo nome de dependência, preservando
  o contrato de resposta atual (FR-007) — em `src/SalesApi.Api/Program.cs` (depende de T003)
- [X] T005 [P] [US1] `docker/Dockerfile`: novo stage `bundle`, a partir do stage `build` já
  existente, gerando o EF Core migration bundle self-contained
  (`dotnet ef migrations bundle --self-contained -r linux-x64 --project
  src/SalesApi.Infrastructure --startup-project src/SalesApi.Api -o /app/efbundle`) — ver
  `research.md`, seção 1
- [X] T006 [US1] `docker/docker-compose.yml`: novo serviço `migrator` construído a partir do
  stage `bundle` (T005), `restart: "no"`, mesma `ConnectionStrings__DefaultConnection` da `api`,
  `depends_on: { postgres: { condition: service_healthy } }`; serviço `api` passa a declarar
  `depends_on: { postgres: { condition: service_healthy }, migrator: { condition:
  service_completed_successfully } }` (depende de T005)
- [X] T007 [P] [US1] Remover a referência ao pacote `AspNetCore.HealthChecks.NpgSql` de
  `src/SalesApi.Api/SalesApi.Api.csproj` — não é mais usado após T004
- [X] T008 [US1] Validar manualmente `quickstart.md`, Cenários 1 e 2 (ambiente limpo com comando
  único, reexecução idempotente, e `/health` reportando schema desatualizado) — checkpoint
  manual, sem teste automatizado dedicado, mesmo padrão de `001-project-setup/tasks.md` para
  tarefas de infraestrutura pura (depende de T004, T006)

**Checkpoint**: US1 completo — `docker compose up -d` a partir de volume limpo resulta em ambiente
operacional na primeira tentativa; `/health` detecta schema desatualizado.

---

## Phase 4: User Story 2 - Valores monetários exatos e estáveis (Priority: P1) 🎯 MVP

**Goal**: `DiscountAmount` e `TotalAmount` de todo `SaleItem` são arredondados em duas casas com
`MidpointRounding.AwayFromZero` no momento do cálculo; o mesmo valor é devolvido no registro/
alteração e em qualquer consulta subsequente; `Sale.TotalAmount` é sempre exatamente a soma dos
totais dos itens ativos.

**Independent Test**: registrar uma venda cujo cálculo de desconto produza fração menor que um
centavo, comparar a resposta do `POST` com a resposta do `GET` subsequente, e confirmar que o
total geral é igual à soma exata dos totais dos itens (ver `quickstart.md`, Cenário 3).

### Tests for User Story 2 ⚠️

- [X] T009 [P] [US2] Teste: `SaleItem` criado com quantidade/preço cujo desconto produz uma
  terceira casa decimal igual a 5 (ponto médio exato) arredonda `DiscountAmount` e `TotalAmount`
  para cima em valor absoluto (`AwayFromZero`) — NOVO em
  `tests/SalesApi.Domain.Tests/Sales/SaleItemTests.cs` (FR-008, FR-010)
- [X] T010 [P] [US2] Teste: `Sale.Create` com múltiplos itens cujos totais individuais foram
  arredondados — `Sale.TotalAmount` é exatamente a soma dos `TotalAmount` dos itens, sem
  diferença de centavo — em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs` (FR-011)
- [X] T011 [P] [US2] Teste: `Sale.Update` recalculando um item cuja quantidade muda de faixa de
  desconto aplica a mesma regra de arredondamento do registro — mesmo arquivo de T010 (FR-012)
- [X] T012 [P] [US2] Teste de integração: `POST /api/sales` seguido de `GET /api/sales/{id}`
  devolve `discountAmount`/`totalAmount` de item e `totalAmount` de venda **idênticos** entre as
  duas respostas, para uma venda cujo desconto produz fração menor que um centavo — em
  `tests/SalesApi.Api.Tests/Sales/CreateSaleEndpointTests.cs` (FR-009, SC-002, SC-003)

### Implementation for User Story 2

- [X] T013 [US2] `SaleItem`: aplicar
  `Math.Round(valor, 2, MidpointRounding.AwayFromZero)` a `DiscountAmount` e `TotalAmount`, tanto
  no construtor privado quanto em `ApplyChange(quantity, unitPrice)` — em
  `src/SalesApi.Domain/Sales/SaleItem.cs` (depende de T009–T012 falhando antes; ver
  `research.md`, seção 3)

**Checkpoint**: US1 e US2 completos — as duas prioridades P1 entregues; o ambiente é provisionável
e os valores monetários são exatos e estáveis.

---

## Phase 5: User Story 3 - Toda falha responde no mesmo contrato (Priority: P2)

**Goal**: qualquer exceção não tratada responde no mesmo envelope `{ "errors": [...] }` já usado
pelas rejeições de regra de negócio, com `500`, sem nenhum detalhe interno no corpo, em qualquer
ambiente de execução — e a causa original é registrada de forma estruturada e correlacionável.

**Independent Test**: provocar uma falha inesperada durante o processamento de uma requisição e
confirmar que a resposta segue o contrato de erro unificado, sem rastro de pilha nem nome de tipo
de exceção (ver `quickstart.md`, Cenário 4).

### Tests for User Story 3 ⚠️

- [X] T014 [P] [US3] Teste de integração: substituindo uma dependência da requisição por um dublê
  que lança exceção (via `ConfigureTestServices` do `WebApplicationFactory`), `POST /api/sales`
  responde `500` com corpo `{ "errors": [{ "key": "server", "message": "..." }] }` — sem stack
  trace, sem nome de tipo de exceção, sem texto da exceção original no corpo, mesmo com
  `ASPNETCORE_ENVIRONMENT=Development` — NOVO em
  `tests/SalesApi.Api.Tests/Infrastructure/ErrorHandlingTests.cs` (FR-013, FR-014)
- [X] T015 [P] [US3] Teste: a mesma falha do T014 é registrada via `ILogger` (substituindo o
  provider de log por um dublê em memória via `ConfigureTestServices`) antes da resposta ser
  devolvida, com o `CorrelationId` da requisição presente no contexto do log — mesmo arquivo de
  T014 (FR-015)

### Implementation for User Story 3

- [X] T016 [US3] `GlobalExceptionHandler : IExceptionHandler` — `TryHandleAsync` loga a exceção
  original via `ILogger<GlobalExceptionHandler>` e escreve o envelope de erro unificado com
  `500`, sempre — NOVO em `src/SalesApi.Api/ErrorHandling/GlobalExceptionHandler.cs` (depende de
  T014, T015 falhando antes; ver `contracts/error-contract.md`)
- [X] T017 [US3] `Program.cs`: registrar
  `builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
  builder.Services.AddProblemDetails();` e `app.UseExceptionHandler()` antes do middleware de
  correlation id existente — em `src/SalesApi.Api/Program.cs` (depende de T016)

**Checkpoint**: US1, US2 e US3 completos — toda resposta de erro, esperada ou não, segue o mesmo
contrato.

---

## Phase 6: User Story 4 - Um produto ocupa uma única linha ao longo de toda a vida da venda (Priority: P2)

**Goal**: uma alteração de venda que tenta reintroduzir, como item novo, um produto que já
pertence à venda — mesmo como item cancelado — é recusada como violação de regra de negócio
(`400`), nunca como falha inesperada de banco (`500`).

**Independent Test**: registrar uma venda com dois itens, cancelar um deles, e solicitar uma
alteração que reintroduza o produto do item cancelado como item novo — confirmando recusa de
negócio identificando o item em conflito, sem mutar nenhum estado (ver `quickstart.md`, Cenário
5).

### Tests for User Story 4 ⚠️

- [X] T018 [P] [US4] Teste: `Sale.Update` com um item novo (sem `id`) cujo produto já pertence a
  um item **cancelado** da mesma venda retorna `Failure` com `Notification` na chave
  `items[{index}].product.id` — em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs` (FR-017,
  FR-018)
- [X] T019 [P] [US4] Teste: `Sale.Update` com um item novo cujo produto **não** pertence a
  nenhum item existente (ativo ou cancelado) da venda continua sendo aceito normalmente —
  regressão do fluxo já existente, mesmo arquivo de T018
- [X] T020 [P] [US4] Teste: uma alteração rejeitada por reintroduzir produto de item cancelado
  não muta nenhum campo da venda nem de seus itens, mesmo padrão dos testes
  `..._SemMutarNada` já existentes — mesmo arquivo de T018 (FR-019)
- [X] T021 [P] [US4] Teste de integração: `PUT /api/sales/{id}` reintroduzindo o produto de um
  item previamente cancelado (via `DELETE /api/sales/{id}/items/{itemId}`) responde `400` com a
  chave do item correspondente no corpo, nunca `500` — em
  `tests/SalesApi.Api.Tests/Sales/UpdateSaleEndpointTests.cs` (SC-005)

### Implementation for User Story 4

- [X] T022 [US4] `Sale.ReconcileNewItem`: comparar o produto do item novo contra o conjunto de
  produtos de **todos** os itens já pertencentes à venda — ativos e cancelados —, não apenas os
  referenciados no corpo da requisição atual; ao detectar conflito, adicionar `Notification` na
  chave `items[{index}].product.id` — em `src/SalesApi.Domain/Sales/Sale.cs` (depende de
  T018–T021 falhando antes; ver `research.md`, seção 5)

**Checkpoint**: US1 a US4 completos — a integridade de INV-03 é garantida pelo domínio antes de
chegar ao banco.

---

## Phase 7: User Story 5 - Documentação de entrada reflete o sistema atual (Priority: P2)

**Goal**: `README.md` descreve o propósito do sistema, as seis operações disponíveis, a regra de
desconto, as decisões de desenho e o passo a passo de preparação do ambiente correspondente ao
comportamento real — sem nenhuma afirmação desatualizada.

**Independent Test**: entregar o repositório a alguém sem contexto prévio e confirmar que, usando
apenas o `README.md`, essa pessoa sobe o ambiente e registra uma venda com sucesso.

### Implementation for User Story 5

> Tarefa de documentação — sem teste unitário dedicado (mesmo padrão de `001-project-setup` para
> README/Docker); validada pelo checkpoint manual T026.

- [X] T023 [US5] `README.md`: reescrever Propósito, Escopo, Arquitetura e Superfície da API
  (as 6 operações: registrar, consultar, listar, alterar, cancelar venda, cancelar item),
  portando o conteúdo já existente no Notion (ver `research.md`, seção 8) — em `README.md`
  (FR-030)
- [X] T024 [US5] `README.md`: acrescentar a política de desconto por faixa de quantidade e as
  decisões de desenho que um leitor questionaria (exclusão lógica via `DELETE`, `External
  Identities` sem cadastro próprio de cliente/filial/produto) — em `README.md`, sequência de
  T023 (FR-032)
- [X] T025 [US5] `README.md`: reescrever a seção de preparação do ambiente cobrindo o fluxo de
  provisionamento via `migrator` (US1) e uma nota explícita sobre o caminho de migration
  recomendado para um ambiente produtivo real (script idempotente revisado manualmente, ver
  `research.md`, seção 1) — em `README.md`, sequência de T024 (depende de T006 estar concluída
  para descrever o fluxo real; FR-031, FR-033)
- [X] T026 [US5] Validar manualmente que uma pessoa sem contexto prévio, seguindo apenas
  `README.md`, consegue subir o ambiente e registrar uma venda com sucesso — checkpoint manual
  (SC-001; depende de T023–T025)

**Checkpoint**: US1 a US5 completos — a documentação de entrada é a primeira impressão correta do
sistema.

---

## Phase 8: User Story 6 - Modelo físico padronizado e diretamente consultável (Priority: P3)

**Goal**: todas as colunas, índices e constraints de `sales` e `sale_items` seguem snake_case,
correspondendo ao Domain Model do Notion; nenhum identificador exige delimitação especial em
consultas manuais; os dados existentes são preservados.

**Independent Test**: inspecionar a estrutura das tabelas e executar consultas manuais sobre
qualquer coluna sem tratamento especial de identificador (ver `quickstart.md`, Cenário 6).

### Tests for User Story 6 ⚠️

- [X] T027 [P] [US6] Teste de integração: uma consulta SQL bruta referenciando `sale_id`,
  `total_amount`, `is_cancelled` e demais colunas renomeadas (ver `data-model.md`) executa sem
  erro e sem delimitação especial, contra o banco migrado via `SalesApiFactory` — NOVO em
  `tests/SalesApi.Api.Tests/Infrastructure/SchemaNamingTests.cs` (FR-020, FR-021)

### Implementation for User Story 6

- [X] T028 [P] [US6] `SaleConfiguration`: `HasColumnName` explícito para `SaleNumber` →
  `sale_number`, `SaleDate` → `sale_date`, `TotalAmount` → `total_amount`, `IsCancelled` →
  `is_cancelled`, `CreatedAt` → `created_at`, `UpdatedAt` → `updated_at`; `HasDatabaseName` do
  índice único de `SaleNumber` para `ix_sales_sale_number` — em
  `src/SalesApi.Infrastructure/Persistence/Configurations/SaleConfiguration.cs` (ver
  `data-model.md`, seção "Modelo físico")
- [X] T029 [P] [US6] `SaleItemConfiguration`: `HasColumnName` explícito para `Quantity` →
  `quantity`, `UnitPrice` → `unit_price`, `DiscountPercentage` → `discount_percentage`,
  `DiscountAmount` → `discount_amount`, `TotalAmount` → `total_amount`, `IsCancelled` →
  `is_cancelled`; shadow property `builder.Property<Guid>("SaleId").HasColumnName("sale_id")`;
  `HasDatabaseName` do índice `SaleId` para `ix_sale_items_sale_id` e da constraint única para
  `uq_sale_product` — em
  `src/SalesApi.Infrastructure/Persistence/Configurations/SaleItemConfiguration.cs`
- [X] T030 [US6] Gerar a migration `RenameColumnsToSnakeCase`
  (`dotnet ef migrations add RenameColumnsToSnakeCase --project src/SalesApi.Infrastructure
  --startup-project src/SalesApi.Api`) refletindo T028/T029 via `RenameColumn`/`RenameIndex` sobre
  o schema criado por `CreateSales`/`AddSalesListIndexes` — sem alterar essas duas migrations
  existentes — em `src/SalesApi.Infrastructure/Persistence/Migrations/` (depende de T028, T029,
  T027 falhando antes)

**Checkpoint**: US1 a US6 completos — o modelo físico corresponde ao documentado, sem exigir
delimitação especial em nenhuma consulta manual.

---

## Phase 9: User Story 7 - Base de código sem resíduos e sem regra duplicada (Priority: P3)

**Goal**: sem tipos de exemplo remanescentes da configuração inicial; validação de
quantidade/preço unitário expressa em um único lugar; tradução `Result` → `IResult` expressa em
um único lugar; `CreateSaleCommandHandler` com logging estruturado; sem aviso de configuração
inconsistente na inicialização.

**Independent Test**: suíte permanece integralmente verde e a cobertura se mantém acima de 90%
após a remoção e a unificação, sem alteração de comportamento observável.

### Tests for User Story 7 ⚠️

- [X] T031 [P] [US7] Teste: `SaleItem.ValidateChange(quantity, unitPrice)` retorna as
  `Notification` esperadas para quantidade acima de 20, quantidade abaixo de 1 e preço unitário
  menor ou igual a zero, com as mesmas chaves/mensagens de `SaleItem.Create` — NOVO em
  `tests/SalesApi.Domain.Tests/Sales/SaleItemTests.cs` (FR-026)
- [X] T032 [P] [US7] Teste: `Sale.Update` (via `ReconcileExistingItem`) continua rejeitando
  quantidade e preço inválidos com as mesmas chaves de hoje após passar a reaproveitar
  `SaleItem.ValidateChange` — regressão, em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`
- [X] T033 [P] [US7] Reescrever `MediatorRegistrationTests.cs` para despachar uma Query real do
  domínio de vendas (ex.: `GetSaleQuery` contra um banco InMemory previamente populado) via
  `IMediator`, sem depender de `PingQuery` — em
  `tests/SalesApi.Application.Tests/Common/MediatorRegistrationTests.cs` (FR-024, FR-025)
- [X] T034 [P] [US7] Reescrever `MapsterConfigurationTests.cs` para exercer um mapeamento real do
  domínio (ex.: `ExternalReferenceRequest` → `ExternalReference`, já registrado por
  `CreateSaleMappingConfig`), sem depender de `SampleSource`/`SampleDestination` — em
  `tests/SalesApi.Application.Tests/Common/MapsterConfigurationTests.cs` (FR-024, FR-025)
- [X] T035 [P] [US7] Teste: `ResultExtensions.ToHttpResult` traduz um `Result` de sucesso para
  `Results.Ok`/`Results.NoContent` (conforme sobrecarga) e um `Result` de falha para
  `Results.NotFound` quando a chave do erro está entre as informadas como "não encontrado", ou
  `Results.BadRequest` caso contrário — NOVO em
  `tests/SalesApi.Api.Tests/Common/ResultExtensionsTests.cs` (FR-027)

### Implementation for User Story 7

- [X] T036 [US7] Extrair `SaleItem.ValidateChange(int quantity, decimal unitPrice)` estático,
  reaproveitando as mesmas mensagens hoje duplicadas — em
  `src/SalesApi.Domain/Sales/SaleItem.cs` (depende de T031 falhando antes; mesmo arquivo de T013,
  edição sequencial)
- [X] T037 [US7] `Sale.ReconcileExistingItem`: substituir a validação manual duplicada de
  quantidade/preço pela chamada a `SaleItem.ValidateChange` — em
  `src/SalesApi.Domain/Sales/Sale.cs` (depende de T036, T032 falhando antes; mesmo arquivo de
  T022, método distinto)
- [X] T038 [US7] Remover `src/SalesApi.Application/Common/PingQuery.cs` e
  `src/SalesApi.Application/Common/SampleMapping.cs` (depende de T033, T034 já exercendo tipos
  reais)
- [X] T039 [P] [US7] `CreateSaleCommandHandler`: adicionar `ILogger<CreateSaleCommandHandler>` via
  construtor, com `LogInformation` em registro bem-sucedido — mesmo padrão dos demais handlers —
  em `src/SalesApi.Application/Sales/Create/CreateSaleCommandHandler.cs` (FR-028)
- [X] T040 [US7] Criar `ResultExtensions.ToHttpResult(this Result result, params string[]
  notFoundKeys)` (e sobrecarga para `Result<T>`) — NOVO em
  `src/SalesApi.Api/Common/ResultExtensions.cs` (depende de T035 falhando antes; ver
  `research.md`, seção 7)
- [X] T041 [US7] `SalesEndpoints`: os 6 endpoints passam a usar `ResultExtensions.ToHttpResult`,
  eliminando a repetição da seleção de `errors` e da decisão `400`/`404`/`404`+`itemId` — em
  `src/SalesApi.Api/Sales/SalesEndpoints.cs` (depende de T040)
- [X] T042 [P] [US7] `Program.cs`: remover `app.UseHttpsRedirection()` — container é HTTP-only,
  gera aviso em todo startup — em `src/SalesApi.Api/Program.cs` (FR-029)

**Checkpoint**: todas as 7 user stories completas — o sistema provisiona de forma confiável,
persiste com exatidão, responde no mesmo contrato de erro, protege suas invariantes, documenta-se
corretamente, e sua base de código está livre de resíduos e de regra duplicada.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T043 [P] Rodar `quickstart.md`, Cenários 3, 4, 5 e 6, manualmente contra o ambiente Docker e
  confirmar cada resposta contra `contracts/error-contract.md`, `contracts/health-check.md` e
  `data-model.md` — validado de forma equivalente pela suíte automatizada de US2, US3, US4 e US6
- [X] T044 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX) após a remoção de scaffolding
  (US7): `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`, sem regressão em nenhuma
  feature anterior (SC-007)
- [X] T045 Build dos 4 projetos + testes sem warnings, respeitando `TreatWarningsAsErrors=true`
  de `Directory.Build.props` — `dotnet build SalesApi.sln`, 0 Warning(s), 0 Error(s); confirmar
  que a remoção de `UseHttpsRedirection` (T042) eliminou o aviso de startup observado nos logs da
  suíte (SC-008, FR-029)
- [X] T046 Critério de aceite geral da spec: `docker compose -f docker/docker-compose.yml down -v`
  seguido de `up -d` a partir de volume limpo, confirmando `POST /api/sales` bem-sucedido de
  ponta a ponta — validação final combinada de US1, US2 e US6 (depende de todas as fases
  anteriores)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem tasks — nada bloqueia o início da Phase 3
- **Foundational (Phase 2)**: sem tasks — nada bloqueia o início da Phase 3
- **User Stories (Phase 3–9)**: sem dependência funcional de implementação entre si — podem ser
  feitas em qualquer ordem, ou em paralelo por pessoas diferentes; a ordem de prioridade (P1, P1,
  P2, P2, P2, P3, P3) é a ordem sugerida de execução sequencial. Única exceção de conteúdo: T025
  (US5) descreve o fluxo de US1 e por isso depende de T006 estar concluída.
- **Polish (Phase 10)**: depende de todas as user stories desejadas estarem completas; T046
  depende especificamente de US1, US2 e US6.

### User Story Dependencies

- **US1 (P1)**: sem dependência de outra user story
- **US2 (P1)**: sem dependência de outra user story
- **US3 (P2)**: sem dependência de outra user story
- **US4 (P2)**: sem dependência de outra user story
- **US5 (P2)**: depende de US1 apenas para a precisão do conteúdo (T025); a estrutura geral do
  README (T023, T024) pode ser escrita a qualquer momento
- **US6 (P3)**: sem dependência de outra user story
- **US7 (P3)**: sem dependência de outra user story — toca os mesmos arquivos de US2 (`SaleItem.cs`)
  e US4 (`Sale.cs`), em métodos distintos; executar depois delas evita apenas retrabalho de merge,
  não é uma dependência funcional

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio II,
  TDD) — exceto US5 e as partes de infraestrutura pura de US1 (Docker/compose), sem teste
  unitário por natureza (ver cabeçalho deste documento)
- Domain antes de Application antes de Infrastructure antes de Api, quando a story atravessa mais
  de uma camada (Princípio V)
- História completa e com checkpoint validado antes de seguir para a próxima, na ordem sugerida

### Parallel Opportunities

- **US1, US2, US3, US4 e US6 podem ser implementadas em paralelo por pessoas diferentes** — cada
  uma toca um conjunto de arquivos praticamente disjunto (ver `Path Conventions`)
- T001, T002 (testes de US1) em paralelo entre si
- T009 a T012 (testes de US2) em paralelo entre si
- T014, T015 (testes de US3) em paralelo entre si
- T018 a T021 (testes de US4) em paralelo entre si
- T028, T029 (configurações de US6) em paralelo entre si
- T031 a T035 (testes de US7) em paralelo entre si
- T043, T044 (Polish) em paralelo entre si

---

## Parallel Example: User Stories 1 e 2 (ambas P1, MVP)

```bash
# Pessoa A — User Story 1 (ambiente):
Task: "Teste de PendingMigrationsHealthCheck (Unhealthy com schema pendente) em tests/SalesApi.Api.Tests/Infrastructure/PendingMigrationsHealthCheckTests.cs"
Task: "PendingMigrationsHealthCheck em src/SalesApi.Infrastructure/HealthChecks/PendingMigrationsHealthCheck.cs"
Task: "Novo stage bundle no docker/Dockerfile"
Task: "Novo serviço migrator no docker/docker-compose.yml"

# Pessoa B — User Story 2 (arredondamento), em paralelo, arquivos totalmente distintos:
Task: "Teste de fronteira de arredondamento em tests/SalesApi.Domain.Tests/Sales/SaleItemTests.cs"
Task: "Teste de Sale.TotalAmount como soma exata em tests/SalesApi.Domain.Tests/Sales/SaleTests.cs"
Task: "Math.Round em SaleItem (construtor e ApplyChange) em src/SalesApi.Domain/Sales/SaleItem.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1 e 2)

1. Completar Phase 3 (US1) e Phase 4 (US2) — as duas prioridades P1
2. **Parar e validar**: rodar `quickstart.md` Cenários 1, 2 e 3 manualmente; confirmar testes de
   US1 e US2 passando
3. Neste ponto o ambiente é provisionável com um comando e os valores monetários são exatos — os
   dois defeitos de maior impacto identificados na revisão estão corrigidos

### Incremental Delivery

1. US1 + US2 → validar isoladamente → ambiente confiável e dados exatos (MVP desta feature)
2. US3 → validar isoladamente → nenhuma falha inesperada expõe detalhe interno
3. US4 → validar isoladamente → INV-03 protegida pelo domínio, não pelo banco
4. US5 → validar isoladamente → README correspondente ao sistema real (melhor escrita após US1)
5. US6 → validar isoladamente → modelo físico consultável sem delimitação especial
6. US7 → validar isoladamente → suíte verde, cobertura mantida, sem resíduo nem duplicação
7. Polish → cobertura, build limpo, validação combinada final (T046)

### Parallel Team Strategy

Diferente de todas as features anteriores (onde as user stories estendiam o mesmo método e tinham
pouco a ganhar com paralelismo), esta feature é a mais adequada de todo o projeto para divisão
entre pessoas diferentes: US1, US2, US3, US4 e US6 tocam conjuntos de arquivos praticamente
disjuntos e não têm dependência funcional entre si. US5 e US7 são naturalmente sequenciais (US5
depende do conteúdo final de US1; US7 toca os mesmos arquivos de US2 e US4 e se beneficia de vir
depois, só para evitar retrabalho de merge).

## Notes

- [P] = arquivos diferentes, ou testes independentes no mesmo arquivo sem dependência entre si
- [US1] a [US7] rastreiam cada task até a user story de `spec.md`
- Rodar os testes e vê-los falhar antes de implementar (Princípio II, TDD), exceto onde
  explicitamente marcado como infraestrutura pura (Docker, `docker-compose.yml`, README)
- T003 (`PendingMigrationsHealthCheck`) precisa de um `PostgreSqlContainer` **não migrado** para
  T001 — diferente de todo o resto da suíte de integração, que usa `SalesApiFactory`
  (sempre migrado); por isso T001/T002 usam um fixture próprio, não a factory compartilhada
- T014/T015 (handler global de exceção) podem usar `ConfigureTestServices` para substituir uma
  dependência por um dublê que lança exceção — evitar depender de timing ou de derrubar o
  container do Postgres, que é frágil e mais lento
- A contagem de testes e a descrição de arquitetura no Guia Técnico do Notion ficam desatualizadas
  após esta feature (novos arquivos de teste, `README.md` reescrito) — atualizar o Notion é um
  follow-up fora do repositório, não uma task desta lista
- Consultar `data-model.md` para a tabela completa de renomeação de colunas antes de implementar
  T028–T030
- Consultar `contracts/error-contract.md` e `contracts/health-check.md` para o formato exato de
  resposta antes de implementar T016 e T003/T004, respectivamente
