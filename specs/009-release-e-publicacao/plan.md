# Implementation Plan: Release Automatizado e Publicação de Imagens

**Branch**: `009-release-e-publicacao` | **Date**: 2026-08-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-release-e-publicacao/spec.md`

## Summary

Feature de entrega contínua, sem caso de uso novo. Fecha o ciclo entre "código correto" e
"artefato que alguém sem acesso ao repositório consegue executar" (US1–US5, FR-001 a FR-036).
Abordagem, por eixo:

- **Versionamento e changelog (US3)**: `release-please-action` em modo `simple`, lendo os
  Conventional Commits já praticados desde o primeiro commit do projeto. Mantém um PR de release
  sempre atualizado em `main`; o merge desse PR cria tag + GitHub Release e gera/atualiza
  `CHANGELOG.md` automaticamente — sem redação manual (ver research.md, seção 1).
- **Publicação de imagens (US1)**: job `publish` em `.github/workflows/ci-cd.yml`, condicionado a
  `release-please` ter criado a release nesta mesma execução — não mais um workflow `cd.yml`
  separado disparado pelo evento `release: published` (decisão original revertida após incidente
  em produção: workflows independentes reagindo ao mesmo push não têm relação de ordem entre si;
  ver research.md, seção 9). O gatilho continua exclusivamente atrelado ao fluxo revisável do
  release-please (FR-005, FR-021). Usa `docker/build-push-action` duas vezes sobre o
  `docker/Dockerfile` já existente, uma por `target` (`runtime` e `migrator`), publicando
  `pedrozulian/mouts-sales-api` e `pedrozulian/mouts-sales-api-migrator` nas tags da versão e
  `latest`, `linux/amd64` apenas (ver research.md, seção 2).
- **Hardening de configuração (US2)**: `ENV ASPNETCORE_ENVIRONMENT=Production` explícito no stage
  `runtime` do Dockerfile; validação fail-fast em `DependencyInjection.cs` que lança
  `InvalidOperationException` nomeando a variável ausente quando
  `ConnectionStrings:DefaultConnection` não é fornecida — antes de `AddDbContext` ser configurado
  (ver research.md, seção 4).
- **Verificação do artefato publicado (US4)**: passo de smoke test no próprio job `publish`, após o
  push das duas imagens — sobe um `postgres:16` como service container do job e roda a imagem
  `-migrator` recém-publicada contra ele, falhando o job se o código de saída não for `0` (ver
  research.md, seção 6).
- **Execução a partir de imagens publicadas (US1)**: `docker/docker-compose.release.yml` novo,
  espelhando a topologia já validada de `docker/docker-compose.yml` (mesma cadeia
  `service_completed_successfully`), trocando `build:` por `image:`. O compose de desenvolvimento
  existente permanece intocado (FR-027).
- **Conformidade de idioma (US5)**: renomeação de `produtoJaPertenceAVenda` → `productAlreadyBelongsToSale`
  em `Sale.cs`, e de `produtoNovo`/`quantidadeInvalida` em `SaleTests.cs`; remoção dos comentários
  supérfluos identificados; emenda ao `.specify/memory/constitution.md` (MINOR, 1.0.1 → 1.1.0)
  esclarecendo a exceção de nomes de método de teste e ampliando a Stack Tecnológica Obrigatória
  com Docker Hub e release-please (ver research.md, seção 7).

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS) — sem alteração de versão.

**Primary Dependencies**: nenhuma dependência nova em nenhum `.csproj` — a validação fail-fast
usa apenas `System.InvalidOperationException` (BCL) e `IConfiguration` (já injetado em
`DependencyInjection.cs`). As adições desta feature vivem fora da árvore de dependências .NET:
`googleapis/release-please-action` e `docker/build-push-action` + `docker/login-action`, todas
consumidas como GitHub Actions no workflow, não como pacote NuGet. Ambas passam a constar da
Stack Tecnológica Obrigatória da constitution (FR-035).

**Storage**: PostgreSQL 16, sem nenhuma alteração de schema — esta feature não adiciona nem
altera migration alguma. O único uso novo de banco é o Postgres efêmero do smoke test (US4),
descartado ao final do job de CD.

**Testing**: xUnit, mesmo padrão de três projetos por camada. Teste novo: validação fail-fast da
connection string ausente (`SalesApi.Api.Tests/Infrastructure`, mesmo diretório de
`PendingMigrationsHealthCheckTests.cs`, por exercer `DependencyInjection.AddInfrastructure` de
forma equivalente). Testes de domínio ajustados apenas por renomeação de identificador (US5) —
comportamento e asserções inalterados. A verificação do pipeline de release e do smoke test do
migrator publicado não é coberta por xUnit — é observável apenas pela execução real do GitHub
Actions (ver quickstart.md, Cenários 4 e 5); não há framework de teste para workflow do Actions
neste projeto, e introduzir um seria complexidade desproporcional ao escopo (ver Complexity
Tracking).

**Target Platform**: GitHub Actions (`ubuntu-latest`) para os workflows novos; imagens publicadas
para `linux/amd64`. Nenhuma mudança na plataforma de execução da aplicação em si.

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já existentes),
sem projeto novo. Assim como a 008, é transversal: toca `docker/`, `.github/workflows/`,
`SalesApi.Infrastructure`, a constitution e o `README.md`, sem adicionar endpoint.

**Performance Goals**: sem meta formal de runtime — a validação fail-fast é uma checagem de
string vazia executada uma única vez na inicialização, custo desprezível. Meta de pipeline: o
smoke test (pull + run do migrator + aplicar migrations em banco vazio) deve concluir em tempo
compatível com um job de CI comum, sem exigir timeout estendido.

**Constraints**: build Release com zero warnings (`TreatWarningsAsErrors`, já vigente); cobertura
mínima de 90% mantida (Princípio IX) — a única lógica de produção nova (validação fail-fast) MUST
ser coberta; nenhuma migration nova; nenhuma mudança de comportamento observável das seis
operações existentes, exceto a exigência de configuração explícita descrita na US2 (SC-009);
`docker/docker-compose.yml` (desenvolvimento, build local) MUST continuar funcionando sem
alteração (FR-027).

**Scale/Scope**: nenhum endpoint novo; 6 endpoints existentes preservados; 1 workflow (`ci-cd.yml`,
consolidando o `ci.yml` preexistente com os `release-please.yml`/`cd.yml` desta feature — 3
arquivos originais viraram 1); 1 compose novo (`docker-compose.release.yml`); 1 arquivo de
produção ajustado (`DependencyInjection.cs`) + 1 linha no Dockerfile; 3 identificadores
renomeados; 1 emenda de constitution; README ganha uma seção.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | Não toca o domínio de vendas — a única mudança em `src/SalesApi.Domain` é a renomeação de um identificador local (US5), sem alteração de comportamento ou de regra. | PASS |
| II. TDD | O teste da validação fail-fast (US2) é escrito antes do ajuste em `DependencyInjection.cs`, seguindo Red-Green-Refactor, como as demais tasks geradas via `/speckit-tasks`. | PASS |
| III. SOLID | Sem impacto — nenhuma classe nova além da validação inline em `DependencyInjection.cs`, que já é o ponto único de composição da Infrastructure. | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md`, contratos, os workflows (comentários) e o `README.md` (seção nova) estão em português; identificadores de código permanecem em inglês — inclusive a própria correção que esta feature aplica (US5, FR-031/FR-032). A emenda ao Princípio IV (FR-033) formaliza a exceção já praticada para nomes de método de teste, sem mudar comportamento algum, apenas removendo a ambiguidade textual. | PASS |
| V. Clean Architecture | A validação fail-fast entra em `SalesApi.Infrastructure` (onde `AddInfrastructure` já compõe o acesso a dados), não em `Api` nem em `Domain` — mantém a direção de dependência existente. | PASS |
| VI. Eventos via Mediator | Sem impacto — nenhum evento novo, nenhuma mudança de despacho. | PASS |
| VII. Result/Notification | Não se aplica à validação fail-fast: ausência de connection string é falha de configuração de infraestrutura na inicialização do processo, não uma rejeição de regra de negócio sobre uma requisição — por isso usa exception (categoria que o próprio Princípio VII reserva a "falhas de infraestrutura, invariantes de programação violadas"), não `Result`. | PASS |
| VIII. Observabilidade | A exception da validação fail-fast é registrada como o próprio motivo de encerramento do processo — visível no log de inicialização/orquestrador, consistente com o padrão de log estruturado já em uso via Serilog. | PASS |
| IX. Qualidade de Código | Nenhuma dependência .NET nova entra no build (Technical Context) — superfície de análise do SonarCloud/SonarQube inalterada. O teste novo (US2) soma à cobertura já exigida. | PASS |
| X. Docker | Esta feature estende o princípio para além do ambiente de desenvolvimento: além de "reprodutível com um comando" localmente, os artefatos passam a ser publicados e executáveis por qualquer pessoa sem o repositório. `docker/docker-compose.yml` (comando único local) permanece intocado; `docker-compose.release.yml` é aditivo. | PASS |

Nenhuma violação identificada. `Complexity Tracking` registra duas decisões de desenho que
merecem justificativa explícita, embora não sejam violação de princípio.

**Reavaliação pós Fase 1**: `data-model.md` não introduz nenhuma tabela, coluna ou entidade de
domínio — as "entidades" descritas são artefatos de pipeline/arquivo. `contracts/` documenta o
contrato de configuração dos artefatos publicados e o contrato observável do pipeline de release,
nenhum dos dois introduz endpoint, mensagem de erro nova ou mudança de contrato da API existente.
A emenda à constitution (US5) é a própria mudança de processo sendo formalizada — o gate
permanece PASS porque a emenda documenta uma prática já adotada (nomes de teste) e uma ampliação
de escopo já esperada pela seção de Governance do próprio documento (troca/ampliação de stack).

## Project Structure

### Documentation (this feature)

```text
specs/009-release-e-publicacao/
├── plan.md                                # Este arquivo (/speckit-plan)
├── research.md                            # Fase 0 (/speckit-plan)
├── data-model.md                          # Fase 1 (/speckit-plan)
├── quickstart.md                          # Fase 1 (/speckit-plan)
├── contracts/
│   ├── image-configuration-contract.md    # Fase 1 — contrato de configuração dos artefatos publicados (US1, US2)
│   └── release-pipeline-contract.md       # Fase 1 — contrato observável do fluxo de release (US3, US4)
├── checklists/
│   └── requirements.md
└── tasks.md                               # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    └── ci-cd.yml                           # AJUSTADO — consolida ci.yml + release-please.yml + cd.yml (antes 3 arquivos, removidos)
                                             #   em jobs sequenciais via needs: build → test → sonar →
                                             #   release-please → publish. Corrige corrida entre CI e CD
                                             #   observada em produção (US1, US3, US4; ver research.md, seção 9)

docker/
├── Dockerfile                              # AJUSTAR — ENV ASPNETCORE_ENVIRONMENT=Production explícito no stage runtime (US2)
├── docker-compose.yml                      # INALTERADO — ambiente de desenvolvimento local, build a partir do código-fonte (FR-027)
└── docker-compose.release.yml              # NOVO — mesma topologia, image: em vez de build: (US1)

src/
└── SalesApi.Infrastructure/
    └── DependencyInjection.cs              # AJUSTAR — valida ConnectionStrings:DefaultConnection não vazia antes de AddDbContext, lança InvalidOperationException nomeando a variável (US2)

src/SalesApi.Domain/
└── Sales/
    └── Sale.cs                             # AJUSTAR — produtoJaPertenceAVenda → productAlreadyBelongsToSale (linha 245); comentário das linhas 239-242 revisado/removido se redundante (US5)

tests/
├── SalesApi.Api.Tests/
│   └── Infrastructure/
│       └── DependencyInjectionTests.cs     # NOVO — ausência de ConnectionStrings:DefaultConnection lança InvalidOperationException com mensagem nomeando a variável (US2)
└── SalesApi.Domain.Tests/
    └── Sales/
        └── SaleTests.cs                    # AJUSTAR — produtoNovo → newProduct, quantidadeInvalida → invalidQuantity; nomes de método de teste (português) preservados (US5)

.specify/
└── memory/
    └── constitution.md                     # EMENDA — 1.0.1 → 1.1.0 (MINOR): exceção de nomes de método de teste no Princípio IV; Docker Hub e release-please na Stack Tecnológica Obrigatória (US5, FR-033, FR-035)

CHANGELOG.md                                # NOVO — gerado e mantido pelo release-please, nunca editado manualmente (US3)

README.md                                   # AJUSTAR — nova seção descrevendo execução a partir de imagens publicadas, ao lado da execução a partir do código-fonte (FR-028, FR-029, FR-030)
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida, sem criar
projeto novo. O centro de gravidade desta feature está fora de `src/` — em `.github/workflows/`,
`docker/` e na documentação de processo — refletindo que é uma feature de entrega, não de domínio.
As únicas mudanças dentro de `src/` são a validação fail-fast (uma peça pequena e isolada em
`Infrastructure`, coerente com o Princípio V) e as renomeações de identificador (US5), sem relação
funcional com o eixo de release. Note-se a ausência de nova migration ou de qualquer alteração de
schema — diferente da 008, esta feature não tem eixo de dados.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Nenhuma violação da constitution identificada. Os itens abaixo não são violações, mas decisões de
desenho que introduzem peças novas de infraestrutura e merecem registro explícito por alterarem a
superfície de automação do projeto:

| Adição | Por que necessária | Alternativa mais simples rejeitada porque |
|---|---|---|
| ~~Dois workflows novos (`release-please.yml`, `cd.yml`) em vez de um único workflow combinado~~ — **revertido, ver nota abaixo** | ~~Separava uma preocupação de baixo risco e frequente (manter o PR de release atualizado) de uma de alto impacto e pouco frequente (publicar imagem)~~ | ~~Um workflow único misturaria os dois ciclos de vida~~ |

> **Nota de revisão**: a linha acima registrava a decisão original de manter `release-please.yml`
> e `cd.yml` como workflows separados de `ci.yml`. Essa decisão foi **revertida em produção**: os
> três workflows, disparados independentemente pelo mesmo push em `main`, não têm garantia de
> ordem entre si — o CD chegou a publicar antes do CI terminar. A separação em arquivos que este
> item defendia como vantagem de auditabilidade era, na prática, a causa estrutural da corrida.
> Os três foram consolidados em `.github/workflows/ci-cd.yml`, com `build → test → sonar →
> release-please → publish` como jobs sequenciais via `needs` — a única forma de expressar "isto
> só roda depois daquilo" para jobs que antes viviam em workflows diferentes. A preocupação
> original sobre `re-run` isolado da publicação é mitigada por `publish` ser o último job da
> cadeia: reexecutar apenas ele (`re-run failed jobs` do próprio Actions) continua possível sem
> re-tocar `build`/`test`/`sonar`. Ver research.md, seção 9, para o relato completo do incidente e
> as alternativas descartadas.
| `docker-compose.release.yml` como arquivo separado, em vez de profiles no `docker-compose.yml` existente | Compose não oferece alternância limpa entre `build:` e `image:` para o mesmo serviço via profile sem duplicar a definição do serviço de qualquer forma (ver research.md, seção 5) — a separação em arquivos é mais legível para quem só quer consumir a imagem publicada, sem precisar entender profiles. | Profiles no arquivo único: evita um segundo arquivo, mas exige que o serviço `api`/`migrator` seja declarado duas vezes de qualquer forma (uma por profile) para trocar `build:` por `image:` — não há economia real de linhas, só perda de clareza sobre qual delas está ativa. |
