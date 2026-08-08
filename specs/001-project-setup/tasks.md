---

description: "Task list template for feature implementation"
---

# Tasks: Configuração Inicial do Projeto

**Input**: Design documents from `/specs/001-project-setup/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/health-check.md, quickstart.md

**Tests**: A constitution (Princípio II — TDD, não negociável) exige testes antes da
implementação para todo código de produção. Tarefas de teste estão incluídas para as
histórias que introduzem lógica de aplicação (US2, US3, US4). Tarefas de infraestrutura pura
(Docker, pipeline de CI, README — US1, US5, US6) são validadas por checkpoints manuais/CI, já
que não são "código" no sentido testável por xUnit.

**Organization**: Tarefas agrupadas por user story (spec.md), na ordem de prioridade
P1 → P2 → P3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode rodar em paralelo (arquivos diferentes, sem dependência entre si)
- **[Story]**: A qual user story a tarefa pertence (US1 a US6)
- Caminhos de arquivo exatos incluídos em cada descrição

## Path Conventions

Estrutura definida em [plan.md](./plan.md): `src/SalesApi.{Domain,Application,Infrastructure,Api}/`,
`tests/SalesApi.{Domain,Application,Api}.Tests/`, `docker/`, `.github/workflows/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Esqueleto do repositório, antes de qualquer projeto .NET existir

- [ ] T001 Criar diretórios `src/`, `tests/`, `docker/`, `.github/workflows/` na raiz do repositório, conforme plan.md
- [ ] T002 Criar `SalesApi.sln` vazio na raiz do repositório
- [ ] T003 [P] Criar `global.json` na raiz fixando a versão do .NET SDK 8.0.x
- [ ] T004 [P] Criar `Directory.Build.props` na raiz habilitando `Nullable`, `ImplicitUsings` e `TreatWarningsAsErrors` para todos os projetos
- [ ] T005 [P] Criar `.editorconfig` na raiz com as regras de estilo de código C#
- [ ] T006 [P] Criar `.gitignore` na raiz cobrindo artefatos de build do .NET e do Docker

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Projetos vazios de cada camada e de cada suíte de teste, compilando, antes de
qualquer user story começar

**⚠️ CRITICAL**: Nenhuma user story pode começar antes desta fase estar completa

- [ ] T007 Criar projeto de biblioteca `SalesApi.Domain` em `src/SalesApi.Domain/SalesApi.Domain.csproj` (net8.0, sem dependências de outros projetos)
- [ ] T008 Criar projeto de biblioteca `SalesApi.Application` em `src/SalesApi.Application/SalesApi.Application.csproj`, referenciando `SalesApi.Domain`
- [ ] T009 Criar projeto de biblioteca `SalesApi.Infrastructure` em `src/SalesApi.Infrastructure/SalesApi.Infrastructure.csproj`, referenciando `SalesApi.Application`
- [ ] T010 Criar projeto Web API `SalesApi.Api` em `src/SalesApi.Api/SalesApi.Api.csproj`, referenciando `SalesApi.Infrastructure` e `SalesApi.Application`
- [ ] T011 [P] Criar projeto de testes xUnit `SalesApi.Domain.Tests` em `tests/SalesApi.Domain.Tests/`, referenciando `SalesApi.Domain` e o pacote `coverlet.collector`
- [ ] T012 [P] Criar projeto de testes xUnit `SalesApi.Application.Tests` em `tests/SalesApi.Application.Tests/`, referenciando `SalesApi.Application` e `coverlet.collector`
- [ ] T013 [P] Criar projeto de testes xUnit `SalesApi.Api.Tests` em `tests/SalesApi.Api.Tests/`, referenciando `SalesApi.Api`, `Microsoft.AspNetCore.Mvc.Testing` e `coverlet.collector`
- [ ] T014 Adicionar os seis projetos ao `SalesApi.sln`
- [ ] T015 Validar que `dotnet build SalesApi.sln` conclui com sucesso com a estrutura de camadas vazia

**Checkpoint**: Solução compila; direção de dependência entre camadas (Princípio V/III da
constitution) já garantida apenas pelas referências de projeto — nenhuma user story pode
violar isso sem quebrar o build.

---

## Phase 3: User Story 1 - Ambiente local sobe com um único comando (Priority: P1) 🎯 MVP

**Goal**: Subir Api, PostgreSQL e SonarQube local com um único comando, sem passos manuais.

**Independent Test**: Em uma máquina limpa (só com Docker), rodar `docker compose up` e
confirmar que os três serviços ficam saudáveis.

### Implementation for User Story 1

- [ ] T016 [US1] Criar `Dockerfile` multi-stage (build + runtime) para `SalesApi.Api` em `docker/Dockerfile`
- [ ] T017 [US1] Criar `docker/docker-compose.yml` orquestrando os serviços `api`, `postgres` (imagem `postgres:16`) e `sonarqube` (imagem `sonarqube:community`), com healthchecks e volumes nomeados
- [ ] T018 [US1] Criar `docker/.env.example` documentando as variáveis de ambiente exigidas pelo `docker-compose.yml` (credenciais do banco, connection string, senha inicial do SonarQube)
- [ ] T019 [US1] Configurar `src/SalesApi.Api/appsettings.json` / variáveis de ambiente para ler a connection string do PostgreSQL a partir do ambiente do `docker-compose.yml`
- [ ] T020 [US1] Validar, seguindo o passo 1 de quickstart.md, que `docker compose up` sobe os três serviços saudáveis sem nenhum passo manual adicional

**Checkpoint**: Ambiente completo sobe com um comando — User Story 1 entregue e testável
independentemente.

---

## Phase 4: User Story 2 - Fundação de código em camadas pronta para receber implementação (Priority: P1)

**Goal**: Camadas com Mediator, Mapster e acesso a dados já plugados; Domínio sem depender de
nenhuma camada externa.

**Independent Test**: Compilar a solução do zero e confirmar, por inspeção das referências de
projeto, que `SalesApi.Domain` não referencia nenhuma outra camada.

### Tests for User Story 2 ⚠️

> **Escrever estes testes primeiro; garantir que falham antes da implementação (Princípio II)**

- [ ] T021 [P] [US2] Escrever teste unitário falhando garantindo que duas instâncias de `Entity` com o mesmo Id são iguais, em `tests/SalesApi.Domain.Tests/Common/EntityTests.cs`
- [ ] T022 [P] [US2] Escrever teste unitário falhando garantindo que `Result.Success()`/`Result.Failure()` expõem corretamente `IsSuccess`/`Errors`, em `tests/SalesApi.Domain.Tests/Common/ResultTests.cs`
- [ ] T023 [P] [US2] Escrever teste de integração falhando garantindo que o `AppDbContext` consegue abrir conexão contra o PostgreSQL, em `tests/SalesApi.Api.Tests/Infrastructure/AppDbContextConnectionTests.cs`
- [ ] T023a [P] [US2] Escrever teste de integração falhando garantindo que um `IRequest` de exemplo (ex.: `PingQuery`) registrado no pipeline é despachado e retorna resposta via `IMediator`, em `tests/SalesApi.Application.Tests/Common/MediatorRegistrationTests.cs`
- [ ] T023b [P] [US2] Escrever teste unitário falhando garantindo que o Mapster mapeia corretamente um objeto de exemplo (ex.: `SampleSource` → `SampleDestination`) configurado via `TypeAdapterConfig`, em `tests/SalesApi.Application.Tests/Common/MapsterConfigurationTests.cs`

### Implementation for User Story 2

- [ ] T024 [P] [US2] Implementar classe base `Entity` (Id, igualdade por identidade) em `src/SalesApi.Domain/Common/Entity.cs` — faz T021 passar
- [ ] T025 [P] [US2] Implementar tipos base `Result`/`Notification` em `src/SalesApi.Domain/Common/Result.cs` — faz T022 passar
- [ ] T026 [P] [US2] Implementar tipo base `DomainEvent` em `src/SalesApi.Domain/Common/DomainEvent.cs`
- [ ] T027 [US2] Adicionar pacote `MediatR` a `SalesApi.Application` e registrá-lo via extensão de DI em `src/SalesApi.Application/DependencyInjection.cs` — faz T023a passar
- [ ] T028 [US2] Adicionar pacotes `Mapster` e `Mapster.DependencyInjection` a `SalesApi.Application` e registrá-los na mesma extensão de DI (`src/SalesApi.Application/DependencyInjection.cs`) — faz T023b passar
- [ ] T028c [US2] Criar interface `IApplicationDbContext` em `src/SalesApi.Application/Common/IApplicationDbContext.cs`, definindo o contrato mínimo de acesso a dados (ex.: `SaveChangesAsync`) que a Infrastructure vai implementar
- [ ] T029 [US2] Adicionar pacote `Npgsql.EntityFrameworkCore.PostgreSQL` e implementar `AppDbContext` (sem DbSets de negócio), implementando `IApplicationDbContext`, em `src/SalesApi.Infrastructure/Persistence/AppDbContext.cs` — faz T023 e T028c passar
- [ ] T030 [US2] Registrar `AppDbContext` como `IApplicationDbContext` na DI (para que a camada Application dependa apenas da interface, nunca da classe concreta), com a connection string vinda de configuração, via extensão de DI em `src/SalesApi.Infrastructure/DependencyInjection.cs`
- [ ] T031 [US2] Ligar as extensões de DI de `Application` e `Infrastructure` em `src/SalesApi.Api/Program.cs`
- [ ] T032 [US2] Validar que `dotnet build SalesApi.sln` continua íntegro e que `SalesApi.Domain` não referencia nenhum outro projeto da solução

**Checkpoint**: Fundação arquitetural completa e testada — User Story 2 entregue e testável
independentemente.

---

## Phase 5: User Story 3 - Fundação de testes automatizados pronta para TDD (Priority: P2)

**Goal**: Suíte de testes descoberta e executável desde o primeiro commit, mesmo sem código
de negócio.

**Independent Test**: Rodar a suíte de testes logo após preparar o ambiente e confirmar
sucesso.

### Implementation for User Story 3

- [ ] T033 [P] [US3] Escrever smoke test confirmando que o host de `SalesApi.Api` sobe sem exceções (via `WebApplicationFactory`) em `tests/SalesApi.Api.Tests/SmokeTests.cs`
- [ ] T034 [US3] Validar, seguindo o passo 3 de quickstart.md, que `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"` executa os três projetos de teste e reporta sucesso

**Checkpoint**: Suíte de testes funcional de ponta a ponta — User Story 3 entregue e testável
independentemente.

---

## Phase 6: User Story 4 - Exploração e verificação de saúde da API (Priority: P2)

**Goal**: Documentação interativa acessível e endpoint `/health` reportando o status da
aplicação e do banco de dados.

**Independent Test**: Com o ambiente no ar, acessar `/swagger` e `/health` diretamente.

### Tests for User Story 4 ⚠️

> **Escrever estes testes primeiro; garantir que falham antes da implementação (Princípio II)**

- [ ] T035 [P] [US4] Escrever teste de integração falhando garantindo que `GET /health` retorna 200 com o check do PostgreSQL saudável quando o banco está acessível, em `tests/SalesApi.Api.Tests/HealthCheckTests.cs`
- [ ] T036 [P] [US4] Escrever teste de integração falhando garantindo que `GET /swagger/v1/swagger.json` retorna 200 e lista o endpoint `/health`, em `tests/SalesApi.Api.Tests/SwaggerTests.cs`

### Implementation for User Story 4

- [ ] T037 [US4] Adicionar pacote `Swashbuckle.AspNetCore` e registrar o middleware de Swagger/OpenAPI em `src/SalesApi.Api/Program.cs` — faz T036 passar
- [ ] T038 [US4] Adicionar pacotes `Microsoft.Extensions.Diagnostics.HealthChecks` e `AspNetCore.HealthChecks.NpgSql`, registrando o health check do PostgreSQL a partir da connection string do `AppDbContext`, em `src/SalesApi.Api/Program.cs` — faz T035 passar
- [ ] T039 [US4] Mapear o endpoint `/health` com formatador de resposta JSON conforme [contracts/health-check.md](./contracts/health-check.md), em `src/SalesApi.Api/HealthChecks/HealthCheckResponseWriter.cs`

**Checkpoint**: API explorável e verificável de ponta a ponta — User Story 4 entregue e
testável independentemente.

---

## Phase 7: User Story 5 - Pipeline de integração contínua com gate de qualidade automatizado (Priority: P3)

**Goal**: Toda alteração proposta é compilada, testada e analisada quanto à qualidade,
bloqueando a integração se a cobertura mínima não for atingida.

**Independent Test**: Abrir uma alteração no repositório e observar o pipeline bloqueando ou
liberando conforme a cobertura.

### Implementation for User Story 5

- [ ] T040 [US5] Criar `.github/workflows/ci.yml` com o step de build (`dotnet restore` + `dotnet build`), disparado em push e pull request
- [ ] T041 [US5] Adicionar ao `.github/workflows/ci.yml` o step de test, rodando `dotnet test --collect:"XPlat Code Coverage"` e publicando o relatório Cobertura como artefato
- [ ] T042 [US5] Adicionar ao `.github/workflows/ci.yml` o step de sonar, instalando o `dotnet-sonarscanner` e executando `begin`/`end` contra o SonarCloud com `sonar.qualitygate.wait=true`, consumindo o secret `SONAR_TOKEN`
- [ ] T043 [US5] Documentar no README.md (ver User Story 6) a configuração única necessária no SonarCloud (chave do projeto, organização, secret) para o pipeline funcionar
- [ ] T044 [US5] Validar, seguindo o passo 6 de quickstart.md, que um Pull Request de teste mostra os checks de build, test e sonar concluindo em até 10 minutos e bloqueando com cobertura abaixo de 90%

**Checkpoint**: Pipeline de qualidade automatizado funcionando — User Story 5 entregue e
testável independentemente.

---

## Phase 8: User Story 6 - Onboarding autoguiado via README (Priority: P3)

**Goal**: Um novo desenvolvedor entende o propósito da aplicação e a coloca para rodar
localmente seguindo apenas o README.

**Independent Test**: Uma pessoa sem contexto prévio segue apenas o README, do zero, e chega
a uma aplicação rodando localmente.

### Implementation for User Story 6

- [ ] T045 [US6] Escrever no `README.md` (raiz do repositório) as seções de propósito da aplicação, stack utilizada e visão geral da arquitetura em camadas
- [ ] T046 [US6] Escrever no `README.md` os pré-requisitos (Docker, .NET SDK) e o passo a passo de preparação do ambiente (`docker compose up`) e execução local
- [ ] T047 [US6] Escrever no `README.md` a seção descrevendo como rodar a suíte de testes e a análise de qualidade local (SonarQube via Docker)
- [ ] T048 [US6] Validar, seguindo o passo 7 de quickstart.md, que uma pessoa sem contexto prévio segue apenas o README e chega à aplicação rodando

**Checkpoint**: Onboarding autoguiado validado — User Story 6 entregue e testável
independentemente.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Melhorias que afetam múltiplas user stories

- [ ] T049 [P] Configurar logging estruturado com `Serilog.AspNetCore` + `Serilog.Sinks.Console`, substituindo o logging padrão em `src/SalesApi.Api/Program.cs`
- [ ] T050 Adicionar enricher/middleware de correlation id ao pipeline do Serilog em `src/SalesApi.Api/Program.cs` (depende de T049 — mesmo arquivo, não paralelizável)
- [ ] T051 Rodar a validação completa de quickstart.md (todos os 7 passos) como checagem final de aceite
- [ ] T052 [P] Revisar a solução contra a tabela de Constitution Check em plan.md e confirmar que nenhuma violação foi introduzida

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Sem dependências — pode começar imediatamente
- **Foundational (Phase 2)**: Depende da conclusão do Setup — BLOQUEIA todas as user stories
- **User Stories (Phase 3-8)**: Todas dependem da conclusão da fase Foundational
  - US1 e US2 (ambas P1) podem rodar em paralelo entre si (arquivos totalmente distintos: Docker vs. código de camadas)
  - US3 e US4 (P2) dependem de US2 já ter plugado Mediator/EF/Api mínima
  - US5 (P3) depende de US1, US2 e US3 (precisa de algo para buildar, testar e medir cobertura)
  - US6 (P3) depende de US1 a US5 (documenta o resultado de todas as outras)
- **Polish (Phase 9)**: Depende de todas as user stories desejadas estarem completas

### User Story Dependencies

- **US1 (P1)**: Após Foundational — sem dependência de outras stories
- **US2 (P1)**: Após Foundational — sem dependência de outras stories
- **US3 (P2)**: Após Foundational; assume que US2 já existe para ter algo além do smoke test do host
- **US4 (P2)**: Após Foundational; assume que US2 já existe (Api mínima + AppDbContext)
- **US5 (P3)**: Após US1, US2 e US3 (precisa de Dockerfile, build íntegro e suíte de testes)
- **US6 (P3)**: Após todas as demais — documenta o resultado final

### Within Each User Story

- Testes (quando incluídos) MUST ser escritos e falhar antes da implementação
- Tipos base antes de configuração de DI
- Configuração de DI antes de integração no `Program.cs`
- Story completa antes de avançar para a próxima prioridade

### Parallel Opportunities

- Todas as tarefas [P] da fase Setup podem rodar em paralelo
- Todas as tarefas [P] da fase Foundational podem rodar em paralelo (T011-T013 entre si)
- US1 e US2 podem ser trabalhadas em paralelo por pessoas diferentes após a fase Foundational
- Testes de uma mesma user story marcados [P] podem rodar em paralelo entre si

---

## Parallel Example: User Story 2

```bash
# Testes da User Story 2, em paralelo:
Task: "Escrever teste unitário de igualdade de Entity em tests/SalesApi.Domain.Tests/Common/EntityTests.cs"
Task: "Escrever teste unitário de Result/Notification em tests/SalesApi.Domain.Tests/Common/ResultTests.cs"
Task: "Escrever teste de integração de conexão do AppDbContext em tests/SalesApi.Api.Tests/Infrastructure/AppDbContextConnectionTests.cs"
Task: "Escrever teste de integração de dispatch via IMediator em tests/SalesApi.Application.Tests/Common/MediatorRegistrationTests.cs"
Task: "Escrever teste unitário de mapeamento via Mapster em tests/SalesApi.Application.Tests/Common/MapsterConfigurationTests.cs"

# Tipos base da User Story 2, em paralelo:
Task: "Implementar Entity em src/SalesApi.Domain/Common/Entity.cs"
Task: "Implementar Result/Notification em src/SalesApi.Domain/Common/Result.cs"
Task: "Implementar DomainEvent em src/SalesApi.Domain/Common/DomainEvent.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Completar Fase 1: Setup
2. Completar Fase 2: Foundational (CRÍTICO — bloqueia todas as stories)
3. Completar Fase 3 (US1) e Fase 4 (US2) — ambiente sobe e fundação arquitetural pronta
4. **PARAR E VALIDAR**: rodar quickstart.md passos 1 e 2 independentemente
5. Esse ponto já representa o MVP da configuração inicial: qualquer pessoa consegue clonar,
   subir o ambiente e começar a desenvolver seguindo TDD sobre uma arquitetura correta

### Incremental Delivery

1. Setup + Foundational → fundação pronta
2. US1 + US2 → ambiente + arquitetura (MVP desta spec)
3. US3 → fundação de testes validada
4. US4 → API explorável e observável
5. US5 → qualidade automatizada no pipeline
6. US6 → onboarding documentado
7. Polish → observabilidade fina e checagem final contra a constitution

### Parallel Team Strategy

Com mais de uma pessoa disponível, após Setup + Foundational:

- Pessoa A: User Story 1 (Docker)
- Pessoa B: User Story 2 (arquitetura em camadas)
- Depois que ambas terminam: Pessoa A segue para US5 (CI), Pessoa B para US3 e US4
- US6 (README) fica por último, feito por quem tiver mais visão do conjunto

---

## Notes

- [P] = arquivos diferentes, sem dependência entre as tarefas
- [Story] mapeia a tarefa à user story correspondente para rastreabilidade
- Tarefas de infraestrutura pura (Docker, pipeline de CI, README) não têm teste unitário
  associado — são validadas por checkpoints manuais/CI, descritos em quickstart.md
- Verificar que os testes falham antes de implementar (Princípio II da constitution)
- Fazer commit após cada tarefa ou grupo lógico de tarefas
- Parar em qualquer checkpoint para validar a story de forma independente
