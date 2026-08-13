# Tasks: Release Automatizado e Publicação de Imagens

**Input**: Design documents from `/specs/009-release-e-publicacao/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md),
[contracts/image-configuration-contract.md](./contracts/image-configuration-contract.md),
[contracts/release-pipeline-contract.md](./contracts/release-pipeline-contract.md),
[quickstart.md](./quickstart.md)

**Tests**: incluídos e obrigatórios onde há código de produção .NET testável — o Princípio II da
constitution (TDD, não negociável) exige teste que falhe antes de qualquer linha de produção
nova. Isso cobre apenas US2 (única story com lógica C# nova). Workflows do GitHub Actions,
Dockerfile, `docker-compose.release.yml`, renomeações de identificador e a emenda à constitution
não têm framework de teste automatizado neste projeto — são validados por checkpoints manuais
equivalentes ao `quickstart.md`, mesmo padrão já adotado em `001-project-setup/tasks.md` e em
`008-confiabilidade-e-consistencia/tasks.md` para tarefas de infraestrutura pura.

**Organization**: tasks agrupadas pelas 5 user stories de `spec.md` (P1, P1, P2, P2, P3). US1 é o
único pré-requisito de conteúdo real para US3 e US4 (nenhuma outra story pode ser publicada ou
verificada sem que artefatos existam) — a spec já registra essa relação explicitamente no "Why
this priority" de US1 e US3. A emenda à constitution passou a viver na Foundational phase (ver
nota de revisão abaixo) — é pré-requisito de **processo**, não de story.

> **Nota de revisão** (`/speckit-analyze`, C1): a versão anterior deste documento colocava a
> emenda à constitution (então T020) na Phase 7 (US5), depois das tasks que adotam Docker Hub e
> release-please como novo provedor de CI/CD (T002/cd.yml, T012/release-please.yml). Isso violava
> a própria constitution — `.specify/memory/constitution.md`, seção "Stack Tecnológica
> Obrigatória": *"Mudanças de stack (troca de biblioteca, banco de dados ou provedor de CI/CD)
> MUST passar por amendment desta constitution antes de serem adotadas"*. A emenda foi movida para
> a Foundational phase (novo T002) e todas as tasks seguintes foram renumeradas. Também
> incorporada nesta revisão: verificação de FR-013 (precedência do parâmetro `--connection` do
> migrator, antes sem nenhuma cobertura) dentro do smoke test de US4, e a dependência de conteúdo
> de T005 (README, US1) sobre T009/T010 (US2) tornada explícita.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, ou testes independentes no mesmo arquivo
  sem dependência entre si)
- **[Story]**: US1 a US5 — mapeado às user stories de `spec.md`
- Caminhos de arquivo exatos em cada descrição

## Path Conventions

Projeto único em Clean Architecture (4 projetos já existentes), sem projeto novo. Diferente da
008, o centro de gravidade desta feature está fora de `src/` — em `.github/workflows/`, `docker/`
e documentação de processo. Dois arquivos são tocados por mais de uma user story, em blocos
distintos, sem conflito funcional:

- `.github/workflows/cd.yml` — US1 (job de build + push das duas imagens), US4 (step de smoke
  test acrescentado ao final do mesmo job)
- `README.md` — US1 (seção de execução a partir de imagens publicadas), US3 (apontamento para
  `CHANGELOG.md`/Releases, acrescentado à mesma seção)

---

## Phase 1: Setup

- [ ] T001 Configurar as credenciais de publicação no GitHub — variável `DOCKERHUB_USERNAME` e
  secret `DOCKERHUB_TOKEN` (access token gerado em Docker Hub → Account Settings → Security, não
  a senha da conta) em Settings → Secrets and variables → Actions do repositório. Ação manual,
  fora do controle de versão — pré-requisito para o job `publish` de `.github/workflows/cd.yml`
  (US1, US4) publicar com sucesso; sem esta configuração, o workflow é criado e versionado
  normalmente, mas falha ao tentar autenticar no Docker Hub (FR-007)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: emenda de processo exigida pela própria constitution antes de qualquer adoção de
ferramenta nova de CI/CD — bloqueia especificamente as tasks que introduzem Docker Hub e
release-please (US1, US3); não bloqueia US2 nem US5, que não dependem de stack nova.

**⚠️ CRITICAL**: T003 (`cd.yml`) e T013 (`release-please.yml`) MUST NOT começar antes de T002
estar concluída — é o próprio texto da constitution que exige a emenda ocorrer "antes de serem
adotadas".

- [X] T002 Emendar `.specify/memory/constitution.md`: (a) Princípio IV ganha frase explícita —
  nomes de método de teste MAY permanecer em português por funcionarem como documentação
  executável de comportamento, não como API do sistema; (b) seção "Stack Tecnológica
  Obrigatória" ganha as entradas "Publicação de imagens: Docker Hub" e "Versionamento e
  changelog: release-please (Conventional Commits)"; atualizar o `Sync Impact Report` no topo do
  arquivo, a versão de `1.0.1` para `1.1.0` e `Last Amended` para a data desta feature — em
  `.specify/memory/constitution.md` (FR-033, FR-035; ver research.md, seção 7). Não depende de
  nenhuma outra task — é edição de texto pura, sem relação de código com as renomeações de US5

**Checkpoint**: a constitution já reflete o novo stack e a exceção de nomes de teste antes de
qualquer workflow que os adote ser criado — Phase 3 pode começar.

---

## Phase 3: User Story 1 - Executar o sistema sem construí-lo (Priority: P1) 🎯 MVP

**Goal**: duas imagens (`pedrozulian/mouts-sales-api` e `pedrozulian/mouts-sales-api-migrator`)
publicáveis no Docker Hub a partir dos targets já existentes do `docker/Dockerfile`, versionadas
juntas; um compose de release que as consome via `image:`; documentação de como executá-las sem
o código-fonte.

**Independent Test**: `quickstart.md`, Cenário 1 — build local com os mesmos targets do CD,
banco em container separado (simulando "outro servidor"), `POST /api/sales` bem-sucedido sem
`git clone`, sem SDK .NET instalado. Não depende do workflow `cd.yml` já ter executado de fato
no GitHub — prova que os artefatos, uma vez existentes, funcionam de forma agnóstica de ambiente.
A observação do disparo automático real do workflow (via evento de Release) só é possível depois
de US3 existir (`quickstart.md`, Cenário 4).

### Implementation for User Story 1

> Sem teste unitário dedicado — infraestrutura de pipeline e documentação, mesmo padrão de
> `008-confiabilidade-e-consistencia` para Docker/compose/README. Validada pelo checkpoint T006.

- [X] T003 [US1] Criar `.github/workflows/cd.yml`: job `publish`, gatilho
  `on: release: { types: [published] }`, steps `docker/login-action@v3` (usando
  `DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN` de T001), dois `docker/build-push-action@v6` a partir de
  `docker/Dockerfile` — um com `target: runtime`, tags
  `pedrozulian/mouts-sales-api:${{ github.event.release.tag_name }}` e
  `pedrozulian/mouts-sales-api:latest`; outro com `target: migrator`, tags
  `pedrozulian/mouts-sales-api-migrator:${{ github.event.release.tag_name }}` e
  `pedrozulian/mouts-sales-api-migrator:latest` — `platforms: linux/amd64`,
  `cache-from`/`cache-to: type=gha`, ambos os builds no mesmo job, em sequência (garante FR-006 —
  falha em qualquer um interrompe o job antes do smoke test, sem publicação parcial) — NOVO em
  `.github/workflows/cd.yml` (FR-001 a FR-004, FR-005, FR-006, FR-007; depende de T002; ver
  research.md, seção 2)
- [X] T004 [P] [US1] Criar `docker/docker-compose.release.yml`: mesma topologia de
  `docker/docker-compose.yml` (serviços `postgres`, `migrator`, `api`, mesma cadeia
  `depends_on`/`condition: service_completed_successfully`), substituindo `build:` por
  `image: pedrozulian/mouts-sales-api-migrator:${TAG:-latest}` e
  `image: pedrozulian/mouts-sales-api:${TAG:-latest}` — NOVO em
  `docker/docker-compose.release.yml` (FR-025, FR-026; ver research.md, seção 5)
- [X] T005 [US1] `README.md`: nova seção descrevendo como obter e executar os artefatos
  publicados (`docker pull`, variáveis de ambiente exigidas — remetendo ao contrato de
  configuração —, uso de `docker/docker-compose.release.yml`), apresentada como alternativa à
  seção existente de execução a partir do código-fonte, sem substituí-la — em `README.md`
  (FR-025, FR-028, FR-029; depende de T003, T004 para os comandos, e de **T009, T010 (US2) para
  descrever corretamente o comportamento de configuração** — connection string obrigatória com
  falha explícita, `ASPNETCORE_ENVIRONMENT=Production` como default — sem essa dependência a
  seção documentaria comportamento ainda não implementado)
- [X] T006 [US1] Validar manualmente `quickstart.md`, Cenário 1 (build local dos dois targets,
  Postgres em container separado, migrator aplicando o schema, `POST /api/sales` bem-sucedido) —
  checkpoint manual (depende de T003, T004, T005)

**Checkpoint**: US1 completo — os artefatos, uma vez publicados, são executáveis por qualquer
pessoa sem acesso ao repositório; falta apenas o gatilho automático (US3) e a verificação prévia
(US4) para o ciclo de publicação real estar fechado.

---

## Phase 4: User Story 2 - Configuração fornecida por quem implanta (Priority: P1) 🎯 MVP

**Goal**: o artefato da aplicação recusa subir sem `ConnectionStrings__DefaultConnection`, com
mensagem nomeando o que falta; `ASPNETCORE_ENVIRONMENT` assume `Production` por default,
sobrescrevível.

**Independent Test**: `quickstart.md`, Cenário 2 — rodar a imagem sem connection string e
confirmar encerramento imediato com mensagem legível; confirmar `ASPNETCORE_ENVIRONMENT=Production`
por default e `Development` quando explicitamente fornecido.

### Tests for User Story 2 ⚠️

- [X] T007 [P] [US2] Teste: `AddInfrastructure` lança `InvalidOperationException` — com mensagem
  citando `ConnectionStrings__DefaultConnection` — quando `configuration.GetConnectionString
  ("DefaultConnection")` é `null`, vazio ou só espaços — NOVO em
  `tests/SalesApi.Api.Tests/Infrastructure/DependencyInjectionTests.cs` (FR-009, FR-010)
- [X] T008 [P] [US2] Teste: `AddInfrastructure` com uma connection string válida não lança
  exceção e registra `AppDbContext` normalmente no `IServiceCollection` — regressão do
  comportamento atual, mesmo arquivo de T007 (FR-008)

### Implementation for User Story 2

- [X] T009 [US2] `DependencyInjection.AddInfrastructure`: validar a connection string com
  `string.IsNullOrWhiteSpace` e lançar `InvalidOperationException` com mensagem nomeando
  `ConnectionStrings__DefaultConnection` e como fornecê-la, antes de `AddDbContext` ser
  configurado — em `src/SalesApi.Infrastructure/DependencyInjection.cs` (depende de T007, T008
  falhando antes; ver research.md, seção 4b)
- [X] T010 [P] [US2] `docker/Dockerfile`: adicionar `ENV ASPNETCORE_ENVIRONMENT=Production`
  explícito no stage `runtime`, antes de `ENV ASPNETCORE_URLS=http://+:8080` — em
  `docker/Dockerfile` (FR-011, FR-012; ver research.md, seção 4a)
- [X] T011 [US2] Validar manualmente `quickstart.md`, Cenário 2 completo (container sem
  connection string encerra imediatamente com mensagem legível; `ASPNETCORE_ENVIRONMENT`
  default `Production`; sobrescrevível para `Development` sem exigir imagem diferente) —
  checkpoint manual (depende de T009, T010)

**Checkpoint**: US1 e US2 completos — as duas prioridades P1 entregues; os artefatos existem,
funcionam fora do repositório e recusam subir mal configurados, com mensagem legível. T005 (US1)
pode agora ser revisada/finalizada com o comportamento real de configuração já implementado.

---

## Phase 5: User Story 3 - Cada versão publicada tem histórico legível e rastreável (Priority: P2)

**Goal**: `release-please` mantém um PR de release sempre atualizado em `main`, calculando o
incremento semântico e gerando `CHANGELOG.md` a partir dos Conventional Commits já praticados; o
merge desse PR cria tag + GitHub Release, gatilho do workflow de US1.

**Independent Test**: `quickstart.md`, Cenário 4, passos 1–5 — commit convencional em `main`
abre/atualiza o PR de release com bump de versão e changelog corretos; merge cria tag e Release.

### Implementation for User Story 3

> Sem teste unitário dedicado — configuração de pipeline e geração de documento, sem framework de
> teste automatizado disponível no projeto para workflows do Actions. Validada pelo checkpoint
> T015.

- [X] T012 [US3] Criar `release-please-config.json` (raiz do repositório): `release-type:
  "simple"`, path `"."`, `changelog-path: "CHANGELOG.md"` — e `.release-please-manifest.json`
  com `{".": "1.0.0"}` como versão inicial (primeira publicação, sem tag prévia) — NOVOS na raiz
  do repositório (FR-016, FR-017; ver research.md, seção 1)
- [X] T013 [US3] Criar `.github/workflows/release-please.yml`: gatilho `push: branches: [main]`,
  `permissions: { contents: write, pull-requests: write }`, step
  `googleapis/release-please-action@v4` referenciando `release-please-config.json` e
  `.release-please-manifest.json` de T012 — NOVO em `.github/workflows/release-please.yml`
  (FR-016, FR-018, FR-019, FR-021; depende de T002; ver research.md, seção 1 e 3)
- [X] T014 [US3] `README.md`: complementar a seção criada em T005 (US1) com um apontamento para
  `CHANGELOG.md` e para as Releases do GitHub como fonte de versões disponíveis — em
  `README.md`, sequência de T005 (FR-020, FR-030; depende de T005, T012, T013)
- [X] T015 [US3] Validar manualmente `quickstart.md`, Cenário 4, passos 1–5, contra um merge real
  em `main` após esta feature ser integrada — checkpoint manual, não reproduzível localmente
  (depende de T012, T013). Validado: PR `chore(main): release 1.1.0` aberto automaticamente após
  o merge da feature, revisado e mesclado manualmente, criando a tag `v1.1.0` e a GitHub Release

**Checkpoint**: US1 a US3 completos — versões passam a ter histórico automático e rastreável; o
gatilho de publicação de US1 passa a ter origem real.

---

## Phase 6: User Story 4 - Publicação só ocorre se o artefato realmente funcionar (Priority: P2)

**Goal**: o job de CD só é considerado bem-sucedido depois de executar o artefato migrator
recém-publicado contra um Postgres efêmero do próprio job — falha nesse passo impede a release de
ser reportada como concluída. Verifica também que o migrator aceita a connection string tanto por
variável de ambiente quanto por parâmetro explícito, com precedência do parâmetro.

**Independent Test**: `quickstart.md`, Cenário 4, passos 6–8, e Cenário 5 — inspecionar o
workflow e confirmar que o smoke test roda depois do push das duas imagens e antes do job ser
marcado como bem-sucedido, sem depender de banco preexistente.

### Implementation for User Story 4

> Sem teste unitário dedicado — verificação ocorre dentro do próprio workflow do Actions, contra
> o artefato publicado. Validada pelo checkpoint T017.

- [X] T016 [US4] `cd.yml`: acrescentar, ao final do job `publish` (após os dois pushes de T003),
  um `services: postgres: image: postgres:16` do próprio job e dois steps de smoke test contra o
  migrator recém-publicado: (a)
  `docker run --rm --network <rede-do-job> -e ConnectionStrings__DefaultConnection="Host=postgres;..."
  pedrozulian/mouts-sales-api-migrator:${{ github.event.release.tag_name }}`, aplicando o schema
  pela variável de ambiente; (b) uma segunda execução do mesmo container, agora **sem** a
  variável de ambiente e passando `--connection "Host=postgres;..."` como argumento — como o
  schema já foi aplicado por (a), essa segunda chamada exercita o caminho idempotente por
  parâmetro explícito, comprovando que o migrator aceita e prioriza `--connection` (FR-013,
  descoberta sem nenhuma cobertura em toda a análise do projeto). Ambos os steps propagam o
  código de saída do container (falha de qualquer um = falha do job) — em
  `.github/workflows/cd.yml`, sequência de T003 (FR-013, FR-022 a FR-024; depende de T003; ver
  research.md, seção 6)
- [X] T017 [US4] Validar manualmente `quickstart.md`, Cenário 4 (passos 6–8) e Cenário 5 contra
  a primeira execução real do workflow após esta feature ser integrada — checkpoint manual
  (depende de T015, T016). Validado: `cd.yml` disparado pelo evento `release: published`
  (run 31655902751), build + push das duas imagens e ambos os smoke tests concluídos com
  `conclusion: success`

**Checkpoint**: US1 a US4 completos — o ciclo de release está fechado: versão determinada
automaticamente, imagens publicadas, artefato verificado (incluindo as duas formas de fornecer
connection string) antes de a publicação ser considerada concluída.

---

## Phase 7: User Story 5 - Base de código em conformidade com a convenção de idioma (Priority: P3)

**Goal**: `produtoJaPertenceAVenda`, `produtoNovo` e `quantidadeInvalida` renomeados para inglês;
comentários supérfluos removidos. A emenda de constitution que esta story motivava já foi feita
em T002 (Foundational) — o registro de decisão permanece aqui.

**Independent Test**: inspecionar a base de código em busca dos três identificadores em
português (deve retornar zero ocorrências) e confirmar que a suíte completa permanece
integralmente verde após a renomeação, sem nenhuma asserção alterada.

### Implementation for User Story 5

> Renomeação pura de identificador local, sem mudança de comportamento observável — não introduz
> lógica nova sujeita a TDD (Princípio II cobre código de produção novo, não refatoração
> comportamentalmente neutra). Validada pela suíte existente permanecendo verde (T020).

- [X] T018 [P] [US5] Renomear `produtoJaPertenceAVenda` → `productAlreadyBelongsToSale` em
  `Sale.ReconcileNewItem` (linha 245, duas ocorrências); revisar o comentário das linhas 239-242
  e removê-lo se a renomeação já tornar o código autoexplicativo, mantendo-o apenas se ainda
  explicar o motivo não óbvio (por que checar contra `_items` inteiro, não só `seenProducts` —
  INV-03) — em `src/SalesApi.Domain/Sales/Sale.cs` (FR-031, FR-034)
- [X] T019 [P] [US5] Renomear `produtoNovo` → `newProduct` (linha 467) e `quantidadeInvalida` →
  `invalidQuantity` (linha 525) em `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs`; nomes de
  método de teste (em português, ex.: `Create_ComUmItem_...`) permanecem inalterados (FR-032,
  FR-033)
- [X] T020 [US5] Rodar a suíte completa (`dotnet test SalesApi.sln`) e confirmar que permanece
  integralmente verde, sem nenhuma asserção alterada — checkpoint manual (depende de T018, T019)

**Checkpoint**: todas as cinco user stories completas — o ciclo de release está fechado e
verificado, e a base de código está em conformidade com a convenção de idioma que a constitution
(já emendada em T002) declara.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T021 [P] Buscar na base de código (`src/`, `tests/`) por qualquer identificador de
  variável/parâmetro remanescente em português além dos já tratados em T018/T019, confirmando
  zero ocorrências (SC-007)
- [X] T022 [P] Confirmar cobertura de testes ≥ 90% (Princípio IX) após os testes novos de US2:
  `dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`, sem regressão em nenhuma feature
  anterior (SC-008, SC-009)
- [X] T023 Build dos 4 projetos + testes sem warnings, respeitando `TreatWarningsAsErrors=true`
  de `Directory.Build.props` — `dotnet build SalesApi.sln`, 0 Warning(s), 0 Error(s) (SC-008)
- [X] T024 Confirmar que `docker/docker-compose.yml` (desenvolvimento, build local) continua
  subindo e operando exatamente como antes desta feature — `docker compose -f
  docker/docker-compose.yml down -v && up -d`, seguido de `POST /api/sales` bem-sucedido
  (FR-027, SC-010)
- [X] T025 Critério de aceite geral da spec: após o merge desta feature em `main` e a primeira
  execução real do ciclo completo (commit → PR de release → merge → tag/Release → CD → smoke
  test → imagens publicadas), confirmar as 10 Success Criteria da spec — validação final
  combinada de todas as user stories (depende de todas as fases anteriores e de T015, T017).
  Confirmado: release `v1.1.0` publicada, imagens `mouts-sales-api` e `mouts-sales-api-migrator`
  publicadas sob a mesma tag, smoke test aprovado antes da conclusão do job (run 31655902751,
  `conclusion: success`)

---

## Phase 9: Ajuste — auto-merge do PR de release

**Goal**: eliminar a última etapa manual do ciclo de release (o clique de merge no PR aberto pelo
release-please), sem abrir mão do ponto de revisão que FR-021 exige. Decisão registrada em
`research.md`, seções 8 e 9, e em `spec.md` (Assumptions).

- [X] T028 Configurar branch protection em `main` (Settings → Branches): required status checks
  `build`, `test`, `sonar`; "Require branches to be up to date before merging" — ação manual do
  usuário, e **pré-requisito funcional** de T026 (sem isso, `--auto` mescla sem esperar nada)
- [X] T026 `release-please.yml`: dar `id: release` ao step do `release-please-action`; novo step
  `Habilita auto-merge no PR de release`, condicionado a
  `steps.release.outputs.prs_created == 'true'`, rodando
  `gh pr merge --auto --merge --repo "${{ github.repository }}" "${{ fromJSON(steps.release.outputs.pr).number }}"`,
  autenticado via `GH_TOKEN: ${{ secrets.RELEASE_PLEASE_TOKEN }}` — em
  `.github/workflows/release-please.yml` (ver research.md, seção 8)

**Nota de revisão**: a primeira implementação (T026/T027 originais) reimplementava a espera pelo
CI via polling manual (`gh run list`) em ambos os workflows — motivada pela branch protection
ainda não estar configurada. Depois de T028 configurada, essa espera passou a ser redundante com
o que o próprio GitHub já garante nativamente: revertida em favor da versão simples acima. T027
(gate equivalente em `cd.yml`) foi removida por completo — desnecessária uma vez que a única porta
de entrada para `main` já exige os três checks (ver research.md, seção 9).

**Checkpoint**: próximo PR de release aberto pelo release-please deve mesclar sozinho assim que
`build`, `test` e `sonar` passarem, sem clique manual e sem nenhum step adicional em `cd.yml` —
observável só no próximo ciclo de release real.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 é manual e externo ao código — não bloqueia o início do trabalho de
  código (T002 em diante pode começar antes de T001 estar concluído), mas bloqueia a execução
  bem-sucedida real de `cd.yml` em CI.
- **Foundational (Phase 2)**: T002 (emenda à constitution) MUST estar concluída antes de T003
  (`cd.yml`) e T013 (`release-please.yml`) — é a própria constitution que exige a emenda antes da
  adoção do novo provedor de CI/CD. Não bloqueia US2 (Phase 4) nem US5 (Phase 7), que não
  dependem de stack nova.
- **User Stories (Phase 3–7)**: US1 (Phase 3) é o único pré-requisito de conteúdo para US3 e US4
  — release-please (US3) só tem o que publicar depois que `cd.yml`/compose de release existirem
  (US1), e o smoke test (US4) só faz sentido depois que o job de publicação existe (US1). US2 e
  US5 são totalmente independentes de todas as outras (além de T002). A ordem sugerida de
  execução sequencial é: T002 (Foundational), depois US1, US2, US3, US4, US5.
- **Polish (Phase 8)**: depende de todas as user stories completas; T025 depende especificamente
  de T015 (US3) e T017 (US4) para observar o ciclo real.

### User Story Dependencies

- **US1 (P1)**: depende apenas de T002 (Foundational)
- **US2 (P1)**: depende apenas de T002 (Foundational); sem dependência de outra user story
- **US3 (P2)**: depende de T002 e de US1 para ter o que descrever (`cd.yml`/compose de release já
  existentes) — a spec já registra essa relação
- **US4 (P2)**: depende de T002 e de US1 (o job `publish` precisa existir antes de receber o step
  de smoke test) e, para observação end-to-end, de US3 (precisa de uma Release real disparando o
  job)
- **US5 (P3)**: depende apenas de T002 (Foundational); sem dependência de outra user story — pode
  ser feita a qualquer momento, inclusive em paralelo com as demais

### Within Each User Story

- Testes MUST ser escritos e vistos falhando antes da implementação correspondente (Princípio II,
  TDD) — aplica-se apenas a US2, única story com código de produção C# novo. As demais são
  infraestrutura de pipeline/documentação/renomeação, validadas por checkpoint manual (ver
  cabeçalho deste documento).
- Domain antes de Infrastructure quando a story atravessa mais de uma camada (Princípio V) — não
  se aplica aqui: cada story toca no máximo uma camada de código C# (US2 → Infrastructure; US5 →
  Domain e Tests).
- História completa e com checkpoint validado antes de seguir para a próxima, na ordem sugerida.

### Parallel Opportunities

- **US2 e US5 podem ser implementadas em paralelo com US1, US3 e US4** — tocam arquivos
  completamente disjuntos (`SalesApi.Infrastructure`/`Dockerfile` e
  `SalesApi.Domain`/`SalesApi.Domain.Tests`, respectivamente) e não têm dependência de conteúdo
  com o eixo de pipeline além de T002.
- T004 (compose de release) em paralelo com T003 (cd.yml) — arquivos diferentes.
- T007, T008 (testes de US2) em paralelo entre si.
- T010 (Dockerfile) em paralelo com T007–T009 — arquivos diferentes.
- T018, T019 (renomeações de US5) em paralelo entre si — arquivos diferentes.
- T021, T022 (Polish) em paralelo entre si.

---

## Parallel Example: User Stories 1 e 2 (ambas P1, MVP, após T002)

```bash
# Pessoa A — User Story 1 (publicação):
Task: "Workflow cd.yml com build+push das duas imagens em .github/workflows/cd.yml"
Task: "docker-compose.release.yml com image: em vez de build:"
Task: "Seção de execução a partir de imagens publicadas em README.md (rascunho, revisar após T009/T010)"

# Pessoa B — User Story 2 (configuração), em paralelo, arquivos totalmente distintos:
Task: "Teste de InvalidOperationException para connection string ausente em tests/SalesApi.Api.Tests/Infrastructure/DependencyInjectionTests.cs"
Task: "Validação fail-fast em src/SalesApi.Infrastructure/DependencyInjection.cs"
Task: "ENV ASPNETCORE_ENVIRONMENT=Production no docker/Dockerfile"
```

---

## Implementation Strategy

### MVP First (User Stories 1 e 2)

1. Completar T002 (Foundational)
2. Completar Phase 3 (US1) e Phase 4 (US2) — as duas prioridades P1
3. **Parar e validar**: rodar `quickstart.md` Cenários 1 e 2 manualmente; confirmar testes de US2
   passando
4. Neste ponto os artefatos existem (mesmo que publicados manualmente pela primeira vez, sem o
   gatilho automático) e recusam subir mal configurados — o essencial de "entregar algo
   executável fora do repositório" já está resolvido

### Incremental Delivery

1. T002 (constitution emendada) → US1 + US2 → validar isoladamente → artefatos publicáveis e bem
   configurados (MVP desta feature)
2. US3 → validar isoladamente (requer merge real em `main`) → versionamento e changelog
   automáticos; o gatilho de US1 passa a ter origem real
3. US4 → validar isoladamente (requer uma Release real) → publicação passa a ser verificada antes
   de concluída, incluindo as duas formas de configurar o migrator
4. US5 → validar isoladamente → base de código em conformidade com a convenção de idioma
5. Polish → cobertura, build limpo, confirmação de que o ambiente de desenvolvimento local segue
   intocado, validação final combinada (T025)

### Parallel Team Strategy

US2 e US5 são as candidatas naturais a paralelismo com o eixo de pipeline (US1, US3, US4): não
compartilham arquivo nem dependência de conteúdo com ele, além de ambas dependerem de T002.
Dentro do próprio eixo de pipeline, US1 precisa vir antes de US3 e US4 por dependência real de
conteúdo (não apenas de convenção de merge, como em outras features) — não é candidata a
paralelismo interno.

## Notes

- [P] = arquivos diferentes, ou tasks independentes no mesmo arquivo sem dependência entre si
- [US1] a [US5] rastreiam cada task até a user story de `spec.md`; T002 não tem label de story
  por ser Foundational
- Rodar os testes de US2 e vê-los falhar antes de implementar (Princípio II, TDD) — as demais
  stories são infraestrutura pura, sem framework de teste disponível para workflows do Actions
  neste projeto
- T015 e T017 (checkpoints de US3 e US4) só são observáveis contra uma execução real do GitHub
  Actions após o merge desta feature em `main` — diferem dos demais checkpoints, que são
  reproduzíveis localmente a qualquer momento
- Consultar `contracts/image-configuration-contract.md` para o formato exato de configuração
  aceito pelos dois artefatos antes de implementar T009
- Consultar `contracts/release-pipeline-contract.md` para a ordem exata de steps esperada dentro
  de `cd.yml` antes de implementar T003 e T016
- A menção a "Docker Hub" e "release-please" como nomes concretos de ferramenta só aparece a
  partir da implementação (T002, T003, T012, T013) — `spec.md` os mantém em nível de resultado,
  seguindo o mesmo critério da 008 (ver `checklists/requirements.md`)
