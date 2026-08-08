# Implementation Plan: Configuração Inicial do Projeto

**Branch**: `001-project-setup` | **Date**: 2026-08-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-project-setup/spec.md`

## Summary

Estabelecer a fundação técnica da Sales API antes de qualquer funcionalidade de negócio:
solução .NET 8 organizada em Clean Architecture (Domain, Application, Infrastructure, Api),
com EF Core + PostgreSQL, MediatR, Mapster, Swagger, logging estruturado (Serilog) e health
check já plugados; fundação de testes xUnit executável desde o primeiro commit; ambiente
Docker (docker-compose) subindo API + PostgreSQL + SonarQube local com um único comando; e
pipeline de CI no GitHub Actions (build → test → SonarCloud) com gate de cobertura mínima de
90%. Nenhuma entidade ou regra de negócio é introduzida nesta fase.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0 (LTS)

**Primary Dependencies**: ASP.NET Core Web API; Entity Framework Core 8 +
`Npgsql.EntityFrameworkCore.PostgreSQL`; `MediatR`; `Mapster` + `Mapster.DependencyInjection`;
`Swashbuckle.AspNetCore` (Swagger/OpenAPI); `Serilog.AspNetCore` + `Serilog.Sinks.Console`;
`Microsoft.Extensions.Diagnostics.HealthChecks` + `AspNetCore.HealthChecks.NpgSql`.

**Storage**: PostgreSQL 16, via Docker. Nesta fase, sem entidades de domínio persistidas —
o `DbContext` existe apenas para provar a conectividade (usado pelo health check).

**Testing**: xUnit + `coverlet.collector` (cobertura, formato Cobertura XML). Cobertura
consumida em CI pelo SonarScanner for .NET (`dotnet-sonarscanner`) e enviada ao SonarCloud.

**Target Platform**: Containers Linux via Docker/Docker Compose (produção do ambiente de
desenvolvimento); execução local via `dotnet run` também suportada.

**Project Type**: web-service (API backend, sem frontend).

**Performance Goals**: N/A nesta fase — sem endpoints de negócio para medir. Meta operacional:
pipeline de CI concluído em até 10 minutos (SC-006 da spec).

**Constraints**: ambiente completo (Api + PostgreSQL + SonarQube local) MUST subir com um
único comando; gate de CI MUST bloquear merge com cobertura < 90%; documentação, comentários
e commits em português; identificadores de código em inglês.

**Scale/Scope**: fundação técnica única — 4 projetos de camada (Domain, Application,
Infrastructure, Api) + 3 projetos de teste xUnit. Sem múltiplos serviços nesta fase.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Status | Nota |
|---|---|---|
| I. DDD | PASS | Camada Domain reservada e isolada; sem regras de negócio ainda (fora de escopo desta spec). |
| II. TDD (não negociável) | PASS | Fundação de testes (US3) existe antes de qualquer código de produção; smoke test segue red-green-refactor. |
| III. SOLID | PASS | Dependências entre camadas via interfaces (DIP); injeção de dependência nativa do ASP.NET Core. |
| IV. Documentação em português | PASS | README, comentários e commits em PT-BR; identificadores em inglês. |
| V. Clean Architecture | PASS | Estrutura de projeto reflete exatamente Domain → Application → Infrastructure → Api, dependências apontando para dentro. |
| VI. Mediator Pattern | PASS | MediatR registrado na composição da Api, pronto para handlers/eventos futuros. |
| VII. Result/Notification Pattern | PASS (fundação) | Sem regras de negócio para validar ainda; classe base `Result`/`Notification` criada no Domain para uso pelas próximas specs. |
| VIII. Observabilidade | PASS | Serilog configurado; endpoint de health check cobre a dependência de banco de dados. |
| IX. Qualidade de código | PASS | Gate de 90% de cobertura no CI via SonarCloud; SonarQube local via Docker para análise antes do PR. |
| X. Ambiente via Docker | PASS | docker-compose orquestra Api + PostgreSQL + SonarQube local com um único comando. |

Nenhuma violação identificada — `Complexity Tracking` não se aplica.

## Project Structure

### Documentation (this feature)

```text
specs/001-project-setup/
├── plan.md              # Este arquivo (/speckit-plan)
├── research.md          # Fase 0 (/speckit-plan)
├── data-model.md         # Fase 1 (/speckit-plan)
├── quickstart.md         # Fase 1 (/speckit-plan)
├── contracts/
│   └── health-check.md   # Fase 1 (/speckit-plan)
└── tasks.md               # Fase 2 (/speckit-tasks — fora do escopo deste comando)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/
│   ├── Common/                     # classes base: Entity, Result/Notification, DomainEvent
│   └── SalesApi.Domain.csproj
├── SalesApi.Application/
│   ├── Common/                     # interfaces de portas (ex.: IApplicationDbContext), behaviors do MediatR
│   └── SalesApi.Application.csproj
├── SalesApi.Infrastructure/
│   ├── Persistence/                 # DbContext e configurações EF Core
│   ├── Logging/                     # configuração do Serilog
│   └── SalesApi.Infrastructure.csproj
└── SalesApi.Api/
    ├── Program.cs                   # composição da aplicação: DI, middlewares, Swagger, health checks
    ├── HealthChecks/
    └── SalesApi.Api.csproj

tests/
├── SalesApi.Domain.Tests/           # testes unitários da camada de domínio
├── SalesApi.Application.Tests/      # testes unitários da camada de aplicação
└── SalesApi.Api.Tests/              # testes de integração (WebApplicationFactory): smoke test e health check

docker/
├── docker-compose.yml                # orquestra Api + PostgreSQL + SonarQube (local)
└── Dockerfile                        # build multi-stage da Api

.github/
└── workflows/
    └── ci.yml                        # build → test → sonar (SonarCloud)

SalesApi.sln
README.md
```

**Structure Decision**: Clean Architecture com 4 projetos de camada em `src/`
(Domain → Application → Infrastructure → Api, dependências apontando para dentro, Princípio
V), testes espelhando cada camada testável em `tests/` (Princípios II e IX), orquestração
Docker centralizada em `docker/` (Princípio X) e pipeline de CI em `.github/workflows/`
(Princípio IX). Opção de projeto único monolítico foi descartada por violar diretamente o
Princípio V (camadas com dependências isoladas).

## Complexity Tracking

*Não se aplica — nenhuma violação da constitution identificada nesta fase.*
