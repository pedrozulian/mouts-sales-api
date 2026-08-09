# mouts-sales-api

API responsável por gerenciar registros de vendas.

## Propósito

A Sales API expõe as operações de negócio para o registro e a consulta de vendas. Esta
etapa inicial do projeto ("configuração inicial") estabelece apenas a fundação técnica —
arquitetura, ambiente e pipeline de qualidade — sobre a qual as próximas funcionalidades de
negócio (CRUD de vendas e regras associadas) serão construídas.

## Stack utilizada

- **Runtime/Framework**: .NET 8.0 (LTS), C# 12, ASP.NET Core Web API.
- **Banco de dados**: PostgreSQL 16, acessado via Entity Framework Core
  (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Mediator**: MediatR, para comandos/queries e eventos de domínio.
- **Mapeamento de objetos**: Mapster.
- **Documentação de API**: Swagger/OpenAPI (Swashbuckle.AspNetCore).
- **Observabilidade**: logging estruturado com Serilog; endpoint `/health` cobrindo a
  dependência de PostgreSQL.
- **Testes**: xUnit + `coverlet.collector` (cobertura em formato Cobertura).
- **Qualidade de código**: SonarCloud no pipeline de CI; SonarQube Community Edition via
  Docker para análise local antes do PR.
- **Containerização**: Docker e Docker Compose para todos os recursos do ambiente (Api,
  PostgreSQL, SonarQube).
- **CI/CD**: GitHub Actions (build → test → sonar).

## Visão geral da arquitetura

O projeto segue Clean Architecture, com dependências apontando sempre para dentro:

```text
SalesApi.Api  →  SalesApi.Infrastructure  →  SalesApi.Application  →  SalesApi.Domain
```

- **`SalesApi.Domain`**: regras de negócio e tipos-base (`Entity`, `Result`/`Notification`,
  `DomainEvent`). Não depende de nenhuma outra camada do projeto.
- **`SalesApi.Application`**: casos de uso, orquestrados via MediatR; contratos de acesso a
  dados (`IApplicationDbContext`); configuração de mapeamento via Mapster.
- **`SalesApi.Infrastructure`**: implementações concretas dos contratos da Application —
  acesso a dados via EF Core (`AppDbContext`) sobre PostgreSQL.
- **`SalesApi.Api`**: composição da aplicação (DI, middlewares), Swagger e health checks.

Cada camada testável possui um projeto de testes correspondente em `tests/`
(`SalesApi.Domain.Tests`, `SalesApi.Application.Tests`, `SalesApi.Api.Tests` — este último
cobrindo também testes de integração via `WebApplicationFactory`).

## Pré-requisitos

- [Docker](https://docs.docker.com/get-docker/) e Docker Compose.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — necessário apenas para
  rodar comandos `dotnet` fora do container (desenvolvimento local).

## Preparando o ambiente

1. Copie o arquivo de variáveis de ambiente de exemplo:

   ```bash
   cp docker/.env.example docker/.env
   ```

2. Suba o ambiente completo (Api + PostgreSQL + SonarQube local) com um único comando:

   ```bash
   docker compose -f docker/docker-compose.yml up -d
   ```

   Isso sobe três containers saudáveis, sem nenhum passo manual adicional:
   - **api** — disponível em `http://localhost:8080` (`/swagger` e `/health`).
   - **postgres** — banco de dados da aplicação.
   - **sonarqube** — análise de qualidade local, em `http://localhost:9000` (login inicial
     `admin`/`admin`, com troca de senha exigida no primeiro acesso).

3. Para rodar a Api localmente fora do Docker (ex.: durante o desenvolvimento com hot
   reload), com o PostgreSQL do passo 2 no ar:

   ```bash
   dotnet run --project src/SalesApi.Api
   ```

## Explorando a API

Com o ambiente no ar:

- **Documentação interativa (Swagger)**: `http://localhost:8080/swagger`.
- **Health check**: `http://localhost:8080/health` — retorna `200 OK` com o status da
  aplicação e do PostgreSQL (contrato completo em
  [specs/001-project-setup/contracts/health-check.md](specs/001-project-setup/contracts/health-check.md)).

## Rodando a suíte de testes

```bash
dotnet test SalesApi.sln --collect:"XPlat Code Coverage"
```

Executa os três projetos de teste (`SalesApi.Domain.Tests`, `SalesApi.Application.Tests`,
`SalesApi.Api.Tests`) e gera o relatório de cobertura em formato Cobertura XML.

> Os testes de integração (ex.: conexão do `AppDbContext`, `/health`) exigem que o
> PostgreSQL do passo 2 acima esteja em execução.

## Análise de qualidade local (SonarQube via Docker)

Com o SonarQube local no ar (passo 2 de "Preparando o ambiente"):

1. Acesse `http://localhost:9000`, faça login (`admin`/`admin`) e troque a senha.
2. Gere um token de análise em **My Account → Security**.
3. Instale o SonarScanner for .NET, caso ainda não tenha:

   ```bash
   dotnet tool install --global dotnet-sonarscanner
   ```

4. Rode a análise apontando para a instância local, a partir da raiz do repositório. As
   propriedades estáveis da análise (onde encontrar o relatório de cobertura, quais pastas
   excluir) ficam centralizadas em [`SonarQube.Analysis.xml`](SonarQube.Analysis.xml), na raiz
   do repositório, e são carregadas via `/s:` — tanto local quanto no CI usam o mesmo arquivo,
   então só o `sonar.host.url` e o `sonar.token` (que mudam por ambiente) continuam na linha de
   comando. O `/s:` precisa de **caminho absoluto**: um caminho relativo é resolvido contra o
   diretório de trabalho interno do scanner (`.sonarqube/`), não contra o diretório onde o
   comando é executado, e o arquivo não é encontrado:

   ```bash
   dotnet sonarscanner begin /k:"mouts-sales-api" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="<seu-token>" /s:"$(pwd)/SonarQube.Analysis.xml"
   dotnet build SalesApi.sln
   dotnet test SalesApi.sln --collect:"XPlat Code Coverage" --results-directory ./coverage
   dotnet sonarscanner end /d:sonar.token="<seu-token>"
   ```

   > Qualquer propriedade passada via `/d:` na linha de comando tem prioridade sobre a mesma
   > propriedade definida em `SonarQube.Analysis.xml` — então dá pra sobrescrever pontualmente
   > sem editar o arquivo.

5. Confira o resultado em `http://localhost:9000`.

## Pipeline de CI (GitHub Actions)

Todo push e Pull Request para `main` dispara o pipeline em
[.github/workflows/ci.yml](.github/workflows/ci.yml), com os steps **build → test → sonar**
(gate de cobertura mínima de 90% via SonarCloud). Falha em qualquer step bloqueia o merge.

Para o step `sonar` funcionar em um novo ambiente, configure uma única vez no repositório:

- **Secret** `SONAR_TOKEN`: token de análise gerado no SonarCloud
  (`Settings → Secrets and variables → Actions`).
- **Variáveis** `SONAR_PROJECT_KEY` e `SONAR_ORGANIZATION`: chave do projeto e organização
  cadastrados no SonarCloud (`Settings → Secrets and variables → Actions → Variables`).
