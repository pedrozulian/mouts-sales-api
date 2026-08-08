# Research: Configuração Inicial do Projeto

Todas as decisões de tecnologia central (linguagem, banco, containerização, provedor de CI,
gate de qualidade) já vinham resolvidas pela constitution do projeto. As decisões abaixo
cobrem as escolhas de bibliotecas concretas que a constitution deixa em aberto.

## Mediator Pattern

- **Decision**: `MediatR`.
- **Rationale**: biblioteca mais madura e amplamente adotada no ecossistema .NET para o
  padrão Mediator; documentação extensa e reconhecida por qualquer avaliador familiarizado
  com projetos .NET. Confirmado explicitamente com o usuário, mesmo estando ciente do modelo
  de licenciamento comercial introduzido a partir da v13 (gratuito para uso individual/
  projetos pequenos como este).
- **Alternatives considered**:
  - `Mediator` (martinothamar) — MIT, baseada em source generators (sem reflection em
    runtime), sem risco de licenciamento futuro. Rejeitada por ser menos conhecida no
    mercado, o que pesa negativamente num projeto de avaliação técnica.
  - Implementação própria (interfaces `IRequestHandler`/`INotificationHandler` resolvidas via
    DI nativo) — descartada por exigir mais código de infraestrutura para manter sem ganho
    relevante nesta fase.

## Logging Estruturado

- **Decision**: `Serilog.AspNetCore` + `Serilog.Sinks.Console`.
- **Rationale**: padrão de fato do ecossistema .NET para logging estruturado; integra
  nativamente com o pipeline de hosting do ASP.NET Core; suporta enrichers (ex.: correlation
  id) que serão úteis conforme o domínio crescer. Sink de console é suficiente nesta fase,
  já que containers tipicamente centralizam logs via stdout.
- **Alternatives considered**: `Microsoft.Extensions.Logging` puro — sem enrichers nem
  sinks estruturados de forma nativa; `NLog` — igualmente viável, porém menos comum em
  projetos novos.

## Acesso a Dados (PostgreSQL + EF Core)

- **Decision**: `Npgsql.EntityFrameworkCore.PostgreSQL`.
- **Rationale**: provedor oficial e mais utilizado para EF Core com PostgreSQL, gratuito e
  com suporte ativo.
- **Alternatives considered**: nenhuma alternativa relevante e gratuita identificada.

## Health Checks

- **Decision**: `Microsoft.Extensions.Diagnostics.HealthChecks` (nativo do ASP.NET Core) +
  `AspNetCore.HealthChecks.NpgSql`.
- **Rationale**: o pacote nativo já cobre a exposição do endpoint; o pacote comunitário
  (MIT, mantido pela comunidade Xabaril) adiciona a verificação específica de conectividade
  com PostgreSQL sem esforço de implementação manual.
- **Alternatives considered**: implementação manual de um `IHealthCheck` customizado —
  descartada por reinventar algo já coberto por um pacote maduro e gratuito.

## Cobertura de Testes e Integração com Sonar

- **Decision**: `coverlet.collector` (coleta de cobertura durante `dotnet test`, formato
  Cobertura XML) + `dotnet-sonarscanner` (SonarScanner for .NET) consumindo o relatório no
  pipeline de CI, publicando no SonarCloud.
- **Rationale**: é o fluxo padrão e documentado oficialmente pela Sonar para projetos .NET;
  integra diretamente com `dotnet test --collect` sem necessidade de ferramentas adicionais
  de conversão de formato.
- **Alternatives considered**: `dotnet-coverage` (ferramenta da Microsoft) — também viável,
  mas `coverlet` tem integração mais direta e documentada com o SonarScanner for .NET.

## Estrutura de Projetos de Teste

- **Decision**: um projeto de teste xUnit por camada testável (`SalesApi.Domain.Tests`,
  `SalesApi.Application.Tests`) mais um projeto de testes de integração para a Api
  (`SalesApi.Api.Tests`, usando `WebApplicationFactory` para validar o smoke test e o
  health check).
- **Rationale**: mantém rastreabilidade de cobertura por camada (alinhado ao Princípio IX —
  Qualidade de Código) e reflete diretamente a separação de Clean Architecture (Princípio V).
- **Alternatives considered**: um único projeto de testes monolítico — mais simples de criar,
  porém mistura testes unitários e de integração e dificulta isolar a cobertura por camada.

## Convenção de Nomenclatura

- **Decision**: namespace raiz `SalesApi`, com sufixo por camada (`SalesApi.Domain`,
  `SalesApi.Application`, `SalesApi.Infrastructure`, `SalesApi.Api`), em inglês.
- **Rationale**: segue o Princípio IV da constitution (identificadores de código em inglês);
  nome curto e alinhado ao propósito do repositório (`mouts-sales-api`).
