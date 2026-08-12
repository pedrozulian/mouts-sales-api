# Implementation Plan: Confiabilidade Operacional e Consistência de Dados

**Branch**: `008-confiabilidade-e-consistencia` | **Date**: 2026-08-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-confiabilidade-e-consistencia/spec.md`

## Summary

Feature de hardening, sem caso de uso novo. Corrige sete defeitos confirmados por execução real
contra a documentação DDD do Notion e alinha o entregue ao que ela descreve (US1–US7, FR-001 a
FR-033). Abordagem, por eixo:

- **Provisionamento (US1)**: um serviço `migrator` novo no `docker-compose.yml`, empacotado como
  [EF Core migration bundle](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#bundles)
  gerado em stage próprio do `Dockerfile`, roda até completar (`dotnet EFBundle`) e aplica as
  migrations contra o Postgres. A `api` passa a depender dele via
  `condition: service_completed_successfully`, preservando `docker compose up -d` como comando
  único (Princípio X). Nenhum `Database.Migrate()` é chamado por `Program.cs` — DDL fica fora do
  processo de runtime da aplicação.
- **Diagnóstico (US1)**: um `IHealthCheck` customizado (`PendingMigrationsHealthCheck`) substitui
  o `AddNpgSql` genérico, reportando `Unhealthy` quando `Database.GetPendingMigrationsAsync()`
  retorna qualquer item — cobre o cenário que hoje reporta `Healthy` com schema inexistente.
- **Precisão monetária (US2)**: `Math.Round(valor, 2, MidpointRounding.AwayFromZero)` aplicado no
  cálculo de `DiscountAmount`/`TotalAmount` dentro de `SaleItem` (construtor e `ApplyChange`) —
  ponto único de cálculo, já reaproveitado por `Sale.Create` e `Sale.Update`. `Sale.TotalAmount`
  soma valores já arredondados, eliminando a divergência entre a resposta de escrita e a de
  leitura.
- **Contrato de erro (US3)**: `UseExceptionHandler` com um handler tipado que traduz qualquer
  exceção não tratada para `{ "errors": [...] }` com `500`, sem vazar detalhe interno, logando a
  exceção original de forma estruturada. Aplicado antes de qualquer middleware de negócio.
- **Integridade de INV-03 (US4)**: `Sale.ReconcileNewItem` passa a considerar **todos** os
  produtos já presentes na venda — ativos e cancelados —, não só os referenciados no corpo da
  requisição, rejeitando com `Notification` (`400`) em vez de deixar o banco rejeitar com
  violação de índice único (`500`).
- **Modelo físico (US6)**: `HasColumnName`/`HasDatabaseName` explícitos em
  `SaleConfiguration`/`SaleItemConfiguration` para as colunas hoje em PascalCase, incluindo a
  shadow property `SaleId`. Uma migration nova (`RenameColumnsToSnakeCase`) usa
  `RenameColumn`/`RenameIndex`/`RenameTable` de constraint sobre o schema existente — sem squash
  das migrations anteriores.
- **Sustentação (US7)**: remoção de `PingQuery`/`SampleMapping` (com reescrita dos dois testes de
  fumaça contra tipos reais do domínio), extração de `SaleItem.ValidateChange` (elimina a
  duplicação de validação em `Sale.ReconcileExistingItem`), extração de um helper
  `ResultExtensions.ToHttpResult` em `SalesApi.Api` (elimina a repetição de tradução `Result` →
  `IResult` nos 6 endpoints), `ILogger` em `CreateSaleCommandHandler`, remoção de
  `UseHttpsRedirection` (container é HTTP-only).
- **Documentação (US5)**: `README.md` reescrito a partir do conteúdo já existente no Notion —
  superfície da API, regra de desconto, decisões de desenho, fluxo de provisionamento do item 1 e
  o caminho de migration recomendado em produção.

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS) — sem alteração de versão.

**Primary Dependencies**: Microsoft.EntityFrameworkCore.Design 8.0.11 e a ferramenta
`dotnet-ef` (já usadas pelo projeto para gerar migrations) passam a ser usadas também para gerar
o migration bundle (`dotnet ef migrations bundle`), consumido apenas no stage de build do
Dockerfile — não é dependência nova de nenhum `.csproj`. `Microsoft.Extensions.Diagnostics.HealthChecks`
(já referenciada via `AddHealthChecks()`) ganha um `IHealthCheck` customizado; `AspNetCore.HealthChecks.NpgSql`
deixa de ser usada como o único check (ver research.md, seção 2) mas permanece referenciada
apenas se ainda fizer sentido combinar os dois sinais — decisão registrada em research.md.
`Microsoft.AspNetCore.Diagnostics` (já parte do SDK ASP.NET Core) fornece `UseExceptionHandler` e
`IExceptionHandler` — nenhum pacote novo. Nenhuma dependência nova é adicionada a nenhum `.csproj`
(EFCore.NamingConventions foi avaliado e descartado — ver research.md, seção 4).

**Storage**: PostgreSQL 16, mesmo schema lógico (`sales`, `sale_items`, `sale_number_seq`). Uma
migration nova (`RenameColumnsToSnakeCase`) renomeia colunas, índices e constraints — não altera
tipos, não altera dados. Nenhuma tabela nova.

**Testing**: xUnit, mesmo padrão de três projetos por camada. Novos testes: fronteira de
arredondamento monetário (`SaleItemTests`/`SaleTests`, Domain), `PendingMigrationsHealthCheckTests`
(Api, com Testcontainers — banco propositalmente não migrado), teste de integração do handler
global de exceção (Api, provocando uma falha de infraestrutura simulada), teste de reintrodução de
produto de item cancelado (Domain, `Sale.Update`), teste de idempotência da migration
(`dotnet ef database update` duas vezes sobre o mesmo banco, coberto pelo próprio
`SalesApiFactory.InitializeAsync` ao migrar a cada execução da suíte). Reescrita de
`MediatorRegistrationTests` e `MapsterConfigurationTests` contra tipos reais do domínio.

**Target Platform**: mesmo ambiente containerizado (Linux, Docker) já orquestrado pelas features
anteriores — `docker-compose.yml` ganha um serviço `migrator` adicional.

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já existentes).
Diferente das features 002–007 (uma feature, um endpoint novo), esta feature é transversal:
toca as quatro camadas, `docker/`, migrations e `README.md`, sem adicionar nenhum endpoint.

**Performance Goals**: sem meta formal — mesmo protótipo de avaliação técnica. O arredondamento e
a checagem de duplicidade adicionam custo de CPU desprezível (operações aritméticas simples e uma
comparação de conjunto já em memória).

**Constraints**: cobertura mínima de 90% (Princípio IX) mantida após a remoção de scaffolding;
build Release com zero warnings (`TreatWarningsAsErrors`); nenhuma migration existente é
reescrita ou squashed (histórico de evolução do schema preservado, ver Assumptions da spec);
nenhuma mudança de comportamento observável na API além das descritas nas User Stories 2, 3 e 4
(FR-023, SC-009); o `docker compose up -d` a partir de volume limpo MUST resultar em
`POST /api/sales` bem-sucedido (critério de aceite geral da spec).

**Scale/Scope**: nenhum endpoint novo; 6 endpoints existentes preservados; 1 serviço novo no
compose; 1 migration nova; ~7 arquivos de produção ajustados ou removidos; README reescrito.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | A correção de INV-03 (US4) e o arredondamento (US2) são implementados dentro do agregado `Sale`/`SaleItem` — nenhuma regra de negócio migra para handler, endpoint ou configuração de EF Core. | PASS |
| II. TDD | Tasks (a gerar via `/speckit-tasks`) seguem Red-Green-Refactor: cada correção de comportamento (US2, US4) ganha teste de fronteira antes do ajuste de produção; a reescrita de `MediatorRegistrationTests`/`MapsterConfigurationTests` (US7) mantém o teste executável a cada passo. | PASS |
| III. SOLID | US7 é, em essência, esta feature aplicando SRP/DRY sobre o próprio projeto: `SaleItem.ValidateChange` elimina uma regra duplicada em dois lugares (violação atual de SRP/DRY), `ResultExtensions.ToHttpResult` elimina repetição estrutural em `SalesEndpoints`. | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md`, contratos e o `README.md` reescrito (US5) estão em português; identificadores de código permanecem em inglês, inclusive os novos (`PendingMigrationsHealthCheck`, `ResultExtensions`). | PASS |
| V. Clean Architecture | Domain recebe o arredondamento e a correção de INV-03; Application recebe o helper de validação reaproveitado e o `ILogger` faltante; Infrastructure recebe a padronização de nomes e a configuração de migração; Api recebe o handler global de exceção, o health check customizado e o helper de tradução `Result → IResult`. Nenhuma camada externa passa a conter regra de negócio. | PASS |
| VI. Eventos via Mediator | Nenhum evento novo, nenhuma mudança na mecânica de despacho. A correção de INV-03 apenas impede que uma tentativa inválida chegue a `SaveChanges` — não afeta o que já é emitido com sucesso. | PASS |
| VII. Result/Notification | US3 e US4 são, ambas, extensões diretas deste princípio: US3 fecha o único buraco onde uma exception de infraestrutura ainda escapava sem tradução; US4 move uma violação de integridade que hoje se manifesta como exception de banco (`DbUpdateException`) para dentro do fluxo `Result`/`Notification` do domínio. | PASS |
| VIII. Observabilidade | US7 adiciona o `ILogger` que faltava em `CreateSaleCommandHandler`; US3 garante que toda exceção não tratada seja logada de forma estruturada e correlacionável antes de virar resposta; US1 adiciona um sinal de saúde que hoje não existe (schema desatualizado). | PASS |
| IX. Qualidade de Código | Critério de aceite geral da spec (SC-007, SC-008) exige suíte verde, cobertura ≥ 90% e build sem warnings ao final — os mesmos gates já aplicados pelo CI. Nenhuma dependência nova entra no build (ver Technical Context), preservando a superfície de análise já configurada no SonarCloud/SonarQube. | PASS |
| X. Docker | É o princípio que esta feature mais diretamente repara: hoje `docker compose up -d` sobe um ambiente inoperante para escrita. O serviço `migrator` restaura a promessa de "um comando" de forma correta — sem exigir passo manual e sem colocar DDL dentro do processo de runtime (ver research.md, seção 1). | PASS |

Nenhuma violação identificada. `Complexity Tracking` registra uma decisão de desenho que merece
justificativa explícita (novo serviço no compose), embora não seja uma violação de princípio.

**Reavaliação pós Fase 1**: `data-model.md` não introduz nenhuma entidade, tabela ou coluna de
negócio nova — apenas renomeia colunas existentes e adiciona arredondamento a campos já
existentes. `contracts/` documenta mudanças de contrato observável (erro genérico, precisão
monetária, novo motivo de rejeição de INV-03) sem introduzir nenhum endpoint. Gate permanece
PASS.

## Project Structure

### Documentation (this feature)

```text
specs/008-confiabilidade-e-consistencia/
├── plan.md                        # Este arquivo (/speckit-plan)
├── research.md                    # Fase 0 (/speckit-plan)
├── data-model.md                  # Fase 1 (/speckit-plan)
├── quickstart.md                  # Fase 1 (/speckit-plan)
├── contracts/
│   ├── error-contract.md          # Fase 1 — contrato de erro unificado (US3)
│   └── health-check.md            # Fase 1 — evolução do contrato de 001-project-setup (US1)
├── checklists/
│   └── requirements.md
└── tasks.md                       # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/
│   └── Sales/
│       ├── Sale.cs                        # AJUSTAR — ReconcileNewItem passa a considerar itens cancelados (INV-03/US4); TotalAmount soma valores já arredondados
│       └── SaleItem.cs                    # AJUSTAR — Math.Round no construtor e em ApplyChange (US2); novo ValidateChange(quantity, unitPrice) estático reaproveitado por Sale.ReconcileExistingItem
│
├── SalesApi.Application/
│   ├── Common/
│   │   ├── PingQuery.cs                   # REMOVER (US7)
│   │   └── SampleMapping.cs               # REMOVER (US7)
│   └── Sales/
│       └── Create/
│           └── CreateSaleCommandHandler.cs # AJUSTAR — adicionar ILogger (US7)
│
├── SalesApi.Infrastructure/
│   ├── HealthChecks/
│   │   └── PendingMigrationsHealthCheck.cs # NOVO — IHealthCheck via Database.GetPendingMigrationsAsync() (US1)
│   ├── DependencyInjection.cs              # AJUSTAR — registra PendingMigrationsHealthCheck em vez de (ou junto com) AddNpgSql
│   └── Persistence/
│       ├── Configurations/
│       │   ├── SaleConfiguration.cs        # AJUSTAR — HasColumnName/HasDatabaseName explícitos, shadow property SaleId (US6)
│       │   └── SaleItemConfiguration.cs    # AJUSTAR — idem
│       └── Migrations/
│           └── <timestamp>_RenameColumnsToSnakeCase.cs  # NOVA — RenameColumn/RenameIndex sobre o schema existente (US6)
│
└── SalesApi.Api/
    ├── Program.cs                          # AJUSTAR — UseExceptionHandler (US3); remover UseHttpsRedirection (US7)
    ├── Common/
    │   └── ResultExtensions.cs             # NOVO — ToHttpResult(Result), elimina repetição em SalesEndpoints (US7)
    ├── Sales/
    │   └── SalesEndpoints.cs               # AJUSTAR — 6 endpoints passam a usar ResultExtensions.ToHttpResult (US7)
    └── ErrorHandling/
        └── GlobalExceptionHandler.cs       # NOVO — IExceptionHandler, traduz exception não tratada para o contrato {errors:[...]} (US3)

docker/
├── Dockerfile                              # AJUSTAR — novo stage `bundle` gerando o EF Core migration bundle
└── docker-compose.yml                      # AJUSTAR — novo serviço `migrator`; `api` depende dele via service_completed_successfully

README.md                                   # REESCRITO (US5)

tests/
├── SalesApi.Domain.Tests/
│   ├── Sales/
│   │   ├── SaleItemTests.cs                # NOVO ou AJUSTAR — fronteira de arredondamento (US2), ValidateChange (US7)
│   │   └── SaleTests.cs                    # AJUSTAR — reintrodução de produto de item cancelado (US4), total com valores arredondados (US2)
│
├── SalesApi.Application.Tests/
│   └── Common/
│       ├── MediatorRegistrationTests.cs    # AJUSTAR — passa a exercer uma Query real do domínio (US7)
│       └── MapsterConfigurationTests.cs    # AJUSTAR — passa a exercer um mapeamento real do domínio (US7)
│
└── SalesApi.Api.Tests/
    ├── HealthCheckTests.cs                 # AJUSTAR — cenário de schema desatualizado reportando Unhealthy (US1)
    ├── Infrastructure/
    │   └── ErrorHandlingTests.cs           # NOVO — exceção não tratada responde no contrato de erro, sem detalhe interno (US3)
    └── Sales/
        └── UpdateSaleEndpointTests.cs      # AJUSTAR — reintrodução de produto de item cancelado responde 400 (US4)
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida (4
projetos), sem criar projeto novo. Diferente de todas as features anteriores — que tocavam
predominantemente uma camada por endpoint novo —, esta feature é deliberadamente transversal: é
uma correção de qualidade sobre o que já existe, não a adição de uma capacidade. Por isso o
`Project Structure` lista ajustes em Domain, Application, Infrastructure e Api simultaneamente,
mais `docker/` e `README.md`, que nenhuma feature anterior precisou tocar.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Nenhuma violação da constitution identificada. O item abaixo não é uma violação, mas é uma peça
de infraestrutura nova (serviço adicional no `docker-compose.yml`) que merece registro explícito
por alterar a topologia do ambiente:

| Adição | Por que necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| Serviço `migrator` no `docker-compose.yml` | É a única forma encontrada de satisfazer simultaneamente FR-001 a FR-005 (schema provisionado, sem efeito colateral do startup da aplicação, comando único) — ver research.md, seção 1. | `Database.Migrate()` dentro de `Program.cs`: mais simples, mas viola FR-003 diretamente e reintroduz os riscos de produção discutidos com o usuário (corrida entre réplicas, privilégio de DDL no usuário de runtime da aplicação, bloqueio de startup) — rejeitada mesmo sendo o caminho de menor esforço. |
