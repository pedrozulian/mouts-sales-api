# Research: Release Automatizado e Publicação de Imagens

**Feature**: `009-release-e-publicacao` | **Date**: 2026-08-12

Este documento resolve as decisões técnicas necessárias para a Fase 1, com base na análise do
estado atual do projeto e nas trocas já registradas com o autor antes da escrita da spec.

## 1. Ferramenta de versionamento e changelog automatizado

**Decision**: [release-please](https://github.com/googleapis/release-please) via
`googleapis/release-please-action`, em modo `simple` (release type genérico, sem manifest
multi-pacote — a solução é um único artefato versionado, não um monorepo).

**Rationale**: O projeto já produz, de forma disciplinada desde o commit inicial, mensagens em
Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`, `test:` — confirmado em `git log`).
release-please lê exatamente esse histórico para (a) determinar o próximo incremento semver e
(b) gerar o `CHANGELOG.md`, sem exigir nenhuma mudança de hábito da equipe. Ele funciona por
**pull request de release**: a cada push em `main`, mantém aberto (ou atualiza) um PR contendo o
bump de versão e o changelog gerado; o merge desse PR é o gatilho que cria a tag e a GitHub
Release. Isso satisfaz diretamente FR-021 (revisável antes de efetivar) e FR-019 (o marco de
versão dispara a publicação) sem trabalho extra de implementação — o PR de release *é* o ponto de
revisão.

**Alternatives considered**:
- **git-cliff**: gera changelog a partir de commits, mas não decide o incremento de versão nem
  gerencia o ciclo de PR/tag — exigiria orquestrar manualmente version bump + tag + changelog em
  um único job, perdendo o ponto de revisão natural que o modelo de PR do release-please oferece
  de graça. Rejeitado por exigir mais peças coordenadas para o mesmo resultado.
- **semantic-release**: equivalente em proposta, mas publica a versão diretamente no push a
  `main` sem PR intermediário — não atende FR-021 (preparação revisável antes de efetivar) sem
  configuração adicional de gate manual. Ecossistema Node-first, mais natural em projetos JS.
- **Tag manual + changelog manual**: descartado na conversa com o usuário — não atende FR-016/
  FR-017 (automação obrigatória) e reintroduz o risco de divergência entre o anunciado e o feito.

**Onde falta versão anterior**: primeira execução do release-please neste repositório. Sem tag
prévia, a ferramenta assume `1.0.0` como ponto de partida (ou o valor de
`bootstrap-sha`/`initial-version` se configurado) e constrói o changelog inicial a partir de todo
o histórico de commits existente — cobre a Assumption da spec sobre a primeira publicação.

## 2. Publicação de duas imagens no Docker Hub via GitHub Actions

**Decision**: `docker/login-action` + `docker/build-push-action`, um job por imagem (ou dois
passos de build no mesmo job), cada um apontando para um `target` distinto do `docker/Dockerfile`
já existente (`runtime` para a API, `migrator` para o bundle). Tags aplicadas: a versão semântica
exata (`X.Y.Z`) e `latest`, para ambas as imagens. Plataforma única: `linux/amd64`.

**Rationale**: O Dockerfile já expõe os dois targets finais corretos (`docker/Dockerfile:41` e
`docker/Dockerfile:26` — ver estado atual do projeto); nenhuma mudança estrutural nele é
necessária para a publicação em si, só para o hardening de ambiente (seção 4). `docker/
build-push-action` é a ação oficial mantida pela Docker Inc., suporta `target:`, cache via GitHub
Actions (`cache-from`/`cache-to: type=gha`) e múltiplas tags na mesma invocação — elimina a
necessidade de lógica de tagging manual via `docker tag`/`docker push`.

**Por que só `linux/amd64`**: o stage `bundle` do Dockerfile roda `dotnet ef migrations bundle
--self-contained --target-runtime <RID>`, que compila artefato nativo por arquitetura. Builds
multi-arquitetura via QEMU emulam a arquitetura estrangeira inteira, e publish self-contained sob
emulação é sabidamente lento (ordem de 10-20x mais lento que nativo para esse tipo de workload).
Como os runners padrão do GitHub Actions (`ubuntu-latest`) são `amd64`, e é essa a arquitetura
também usada nos ambientes de avaliação e desenvolvimento típicos, publicar `arm64` hoje pagaria um
custo alto de tempo de pipeline por um público-alvo que esta entrega não tem. Path de evolução
(registrado como Assumption): usar matriz de runners nativos (`ubuntu-24.04-arm`) em vez de QEMU,
se `arm64` vier a ser necessário.

**Credenciais**: `DOCKERHUB_USERNAME` (variável) e `DOCKERHUB_TOKEN` (secret, access token
gerado em Docker Hub → Account Settings → Security — nunca a senha da conta), configurados no
GitHub repository/environment settings antes da primeira execução do workflow. Satisfaz FR-007.

**Alternatives considered**:
- **GitHub Container Registry (ghcr.io)**: tecnicamente mais simples (usa `GITHUB_TOKEN`, sem
  secret adicional), mas a spec e a conversa prévia fixaram Docker Hub como alvo — é o registro
  publicamente reconhecível por um avaliador sem contexto do GitHub do candidato. Não descartado
  como prática geral, apenas fora do escopo desta feature (ver Assumptions da spec).
- **`docker buildx bake`**: mais expressivo para builds multi-imagem complexos, mas é
  complexidade desnecessária para duas imagens com um Dockerfile só — `build-push-action` cobre o
  caso sem introduzir uma ferramenta de configuração nova.

## 3. Gatilho da publicação: Release do release-please, não push em tag isolado

**Decision**: O workflow de publicação (`cd.yml`) escuta o evento `release: types: [published]`
emitido pelo `release-please-action` quando o PR de release é mesclado — não um `push: tags:`
genérico.

**Rationale**: Usar o evento `release published` (em vez de `push` em tags `v*`) acopla a
publicação exclusivamente ao fluxo controlado do release-please (FR-005: gatilho exclusivo pelo
marco de versão), e não a qualquer tag criada manualmente. Evita o cenário de alguém criar uma
tag `v9.9.9` manualmente e disparar uma publicação fora do histórico de changelog gerado. O
`release-please-action` expõe `outputs.tag_name` e `outputs.releases_created`, que o job de CD
usa diretamente para nomear as tags de imagem, sem precisar re-parsear a tag do evento.

**Alternatives considered**:
- **`push` em `main` direto**: publicaria a cada commit, violando FR-005 explicitamente
  (histórico de imagem viraria ruído, sem relação com uma versão semântica coerente).
- **`push` em tags `v*`**: funcional, mas desacopla o gatilho da revisão do PR de release —
  qualquer tag manual dispararia publicação, contornando o ponto de controle que FR-021 pede.

## 4. Hardening da imagem para consumo externo

### 4a. `ASPNETCORE_ENVIRONMENT` default

**Decision**: `ENV ASPNETCORE_ENVIRONMENT=Production` explícito no stage `runtime` do
`docker/Dockerfile`. O `docker/docker-compose.yml` (ambiente de desenvolvimento local) continua
sobrescrevendo para `${ASPNETCORE_ENVIRONMENT:-Development}` — o opt-in para o perfil de
desenvolvimento permanece ali, deliberado e visível.

**Rationale**: O ASP.NET Core já resolve para `Production` na ausência da variável — mas
declará-la explicitamente na imagem torna a intenção auditável e imune a uma mudança futura no
comportamento default do framework ou da imagem base. Satisfaz FR-011/FR-012. Verificado: nada em
`Program.cs` (Swagger incluído) depende condicionalmente do valor de `IWebHostEnvironment` — a
troca de default não desliga nenhuma feature em uso pelo avaliador.

### 4b. Falha explícita quando a connection string está ausente

**Decision**: Validação em `DependencyInjection.cs` (`SalesApi.Infrastructure`), lançando
`InvalidOperationException` com mensagem que nomeia a variável de ambiente esperada, antes de
`AddDbContext` ser configurado.

**Rationale**: Hoje `appsettings.json` define `ConnectionStrings:DefaultConnection` como string
vazia e `UseNpgsql` a aceita sem validar — o erro só se manifesta na primeira query, como exceção
de conexão Npgsql sem relação óbvia com "faltou configurar". Uma exception lançada no `Startup`
interrompe a inicialização do container antes de ele começar a aceitar tráfego (satisfaz FR-009,
FR-010): o orquestrador vê o container falhar e reiniciar/reportar erro imediatamente, com
mensagem legível nos logs — não uma falha silenciosa mascarada por um healthcheck que ainda não
rodou.

**Descoberta durante a implementação (achado do smoke test, T016/US4)**: a validação acima, sem
qualificação, quebra o `--connection` do migrator (FR-013). O bundle self-contained
(`dotnet ef migrations bundle`) invoca `Program.Main` da própria `SalesApi.Api` via
`HostFactoryResolver` para descobrir o `AppDbContext` — e só substitui a connection string pelo
`--connection` informado *depois* que esse host termina de ser construído com sucesso
(`MigrationsOperations.UpdateDatabase(targetMigration, connectionString, contextType)`, já com o
`DbContext` criado). Se `AddInfrastructure` lançasse antes disso, `--connection` nunca teria a
chance de ser aplicado — confirmado empiricamente: com a validação sem qualificação, `--connection`
sozinho (sem a env var) falhava sempre com a mesma mensagem de configuração ausente.

A hipótese inicial de usar `EF.IsDesignTime` (o sinal oficial do EF Core para distinguir execução
de tooling) foi testada e **descartada**: esse flag só é propagado pelo driver `dotnet-ef`
(pacote `Microsoft.EntityFrameworkCore.Tools`), não pelo executável self-contained do bundle
(`Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsBundle`) — confirmado via probe de
diagnóstico no próprio artefato: `EF.IsDesignTime=False` mesmo dentro da resolução de host do
bundle.

**Decisão final**: o stage `migrator` do Dockerfile define `ENV SalesApi__Artifact=migrator`,
lido em `AddInfrastructure` via `configuration["SalesApi:Artifact"]`. A validação fail-fast só se
aplica quando esse valor não é `"migrator"` — sinal explícito e controlado pelo próprio projeto,
independente de mecanismos internos do EF Core que se mostraram não confiáveis para este cenário.
Os quatro comportamentos abaixo foram verificados com as imagens reais (build local, não só
testes unitários):

| Cenário | Resultado |
|---|---|
| Migrator, só `--connection`, sem env var | Aplica o schema, exit 0 |
| Migrator, sem env var e sem `--connection` | Falha com erro claro do Npgsql, exit ≠ 0 (nunca trava) |
| Migrator, só env var (fluxo do compose) | Aplica o schema, exit 0 — comportamento preexistente preservado |
| Api, sem env var | Encerra imediatamente com a mensagem fail-fast, exit ≠ 0 — FR-009/FR-010 intactos |

**Alternatives considered**:
- **`IStartupFilter`/`IValidateOptions<T>` com validação de Options pattern**: mais idiomático
  para configuração estruturada, mas a connection string já é lida hoje como valor primitivo via
  `IConfiguration.GetConnectionString`, e introduzir Options pattern só para isso seria
  refatoração desproporcional ao problema. Rejeitado por complexidade não justificada.
- **`EF.IsDesignTime`**: testado e descartado — ver descoberta acima.
- **Detectar via assembly de entrada ou argumentos de processo** (`Assembly.GetEntryAssembly()`,
  `Environment.GetCommandLineArgs()`): funcionaria para distinguir o bundle (`EntryAssembly=migrate`
  no probe), mas amarra a lógica a um nome de arquivo de saída escolhido arbitrariamente no
  Dockerfile (`--output .../migrate`) e, mais grave, também exigiria tratamento especial para não
  quebrar os próprios testes unitários (que rodam sob um host de teste, não sob `SalesApi.Api`).
  Rejeitado por acoplar comportamento de produção a um detalhe de nome de arquivo de build.
- **Health check reportando Unhealthy**: já existe `PendingMigrationsHealthCheck`, mas ele só é
  alcançável **depois** que `AddDbContext` e o startup completam com sucesso — não impede a
  inicialização, só a reporta como não saudável depois de já estar no ar. Não substitui a
  validação fail-fast, é complementar a ela.

## 5. `docker-compose.release.yml`

**Decision**: Novo arquivo `docker/docker-compose.release.yml`, estruturalmente análogo ao
`docker/docker-compose.yml` existente, mas com `image: pedrozulian/mouts-sales-api:${TAG:-latest}`
(e `-migrator` equivalente) em vez de `build:`. Reaproveita o mesmo `postgres` e a mesma cadeia de
`depends_on`/`condition: service_completed_successfully` já validada pelo compose de
desenvolvimento — só troca a origem da imagem.

**Rationale**: Satisfaz FR-025/FR-026 sem duplicar a topologia de dependências que já funciona.
Mantém `docker/docker-compose.yml` (build local) intacto — FR-027 exige que a forma de execução a
partir do código-fonte continue existindo sem alteração.

**Alternatives considered**:
- **Um único `docker-compose.yml` com `image`/`build` condicionais via profile**: Compose não
  suporta alternância limpa entre `build:` e `image:` para o mesmo serviço via profile sem
  duplicar a definição do serviço de qualquer forma — a separação em dois arquivos é mais
  legível e não exige que quem só quer rodar a imagem publicada entenda a sintaxe de profiles.

## 6. Smoke test do artefato publicado

**Decision**: Passo adicional no job de CD, após o push das imagens: subir um serviço
`postgres:16` efêmero (job service container do GitHub Actions) e rodar
`docker run --rm --network <net> -e ConnectionStrings__DefaultConnection=... <imagem-migrator-recém-publicada>`,
falhando o job (interrompendo a publicação da release como "concluída com sucesso") se o
container de migração não sair com código 0.

**Rationale**: Cobre exatamente a classe de defeito que a spec identifica na User Story 4 —
incompatibilidade de arquitetura do bundle self-contained, arquivo ausente na montagem — que só
se manifesta no artefato final e não no código-fonte. Usar um service container do próprio job
(em vez de subir compose completo) mantém a verificação sem dependência de infraestrutura
preexistente e sem resíduo após o job encerrar (FR-023) — o runner é descartado ao final de
qualquer forma.

**Alternatives considered**:
- **Rodar o compose de release completo (API + migrator) no CD**: verificaria também a API, mas
  API já é exercida de ponta a ponta pelos testes de integração do CI (Testcontainers) antes da
  publicação — o gap real está apenas no bundle self-contained, que nenhum teste hoje executa
  como binário standalone. Rejeitado por redundância desproporcional ao ganho.

## 7. Emenda ao documento de princípios

**Decision**: Duas alterações no `.specify/memory/constitution.md`, uma única emenda com bump
MINOR (1.0.1 → 1.1.0):

1. Seção **Stack Tecnológica Obrigatória** ganha duas entradas: "Publicação de imagens: Docker
   Hub" e "Versionamento e changelog: release-please (Conventional Commits)".
2. Princípio **IV. Documentação e Comunicação em Português** ganha uma frase esclarecendo que
   nomes de método de teste são exceção documentada — permanecem em português por funcionarem
   como documentação executável de comportamento, não como API do sistema.

**Rationale**: A própria seção de Governance do documento exige emenda formal para "troca de
stack" e classifica isso como MINOR ("adição de novo princípio ou seção, ou expansão material de
uma diretriz existente") — a adição de duas ferramentas à lista obrigatória se enquadra
literalmente aí. O esclarecimento sobre testes, isoladamente, seria PATCH; como as duas mudanças
saem juntas no mesmo commit de emenda, a versão resultante segue a maior das duas (MINOR).

## 8. Auto-merge do PR de release, condicionado ao CI

**Decision**: passo adicional em `release-please.yml`, após a criação/atualização do PR de
release, habilitando `gh pr merge --auto --merge` sobre esse PR — condicionado a
`steps.release.outputs.prs_created == 'true'`. O merge só se efetiva quando os status checks
obrigatórios (`build`, `test`, `sonar`) passarem, via **branch protection em `main`** exigindo
exatamente esses três checks e "Require branches to be up to date before merging" — pré-requisito
funcional desta decisão, não apenas defesa em profundidade.

**Rationale**: FR-021 exige que a preparação de uma nova versão seja revisável antes de ser
efetivada — mas "revisável" não exige clique manual obrigatório em toda release, apenas que
exista uma janela em que a versão determinada e o changelog gerado possam ser inspecionados e,
se necessário, interrompidos antes de virarem tag/Release. Auto-merge condicionado aos checks
obrigatórios preserva exatamente essa janela (do momento em que o PR é aberto/atualizado até os
checks concluírem), sem exigir intervenção humana no caminho feliz — remove a única etapa manual
restante do fluxo de release sem abrir mão do ponto de revisão.

**Alternatives considered**:
- **Espera explícita via polling em `gh run list`, com merge manual só depois** (tentativa
  intermediária, revertida): implementada e testada antes desta decisão final — cerca de 25
  linhas de bash reimplementando, dentro do workflow, exatamente o que a fila de merge nativa do
  GitHub já resolve. Motivada pela preocupação de que `--auto` mesclasse sem esperar nada caso a
  branch protection não estivesse configurada — mas essa é uma lacuna de configuração, não algo
  que se resolve com mais código. Revertida assim que a branch protection foi configurada
  corretamente (ver Governança abaixo), restaurando a versão simples.
- **Manter o merge manual obrigatório**: mantém a revisão humana em 100% das releases, mas
  reintroduz a necessidade de alguém lembrar de mesclar o PR — rejeitada porque o objetivo
  explícito desta mudança é eliminar exatamente essa dependência de ação manual, mantendo o PR
  como registro auditável.
- **Trocar release-please por uma ferramenta sem PR intermediário** (ex.: semantic-release, que
  publica a release direto no push): eliminaria a etapa de PR por completo, mas descartaria o
  ponto de revisão que FR-021 exige e obrigaria trocar de ferramenta — desproporcional ao
  problema, que é só "automatizar o clique", não "eliminar a revisão".
- **Squash em vez de merge commit**: tecnicamente equivalente para este repositório (pacote
  único na raiz, sem monorepo), mas merge commit é o padrão recomendado pelos mantenedores do
  release-please para o PR de release especificamente — mantido por ser o caminho testado pela
  própria ferramenta, e por já ser o método usado nos merges anteriores deste repositório.

**Governança**: `main` exige os status checks `build`, `test` e `sonar` antes de qualquer merge
(incluindo "Require branches to be up to date before merging") — configurado em Settings →
Branches, ação manual do usuário, fora do controle de versão. Sem essa configuração, `--auto`
mescla sem esperar nada; com ela, é o próprio GitHub que garante a espera, sem código adicional.

## 9. CD não precisa de um gate próprio esperando o CI

**Decision**: `cd.yml` permanece disparando só em `release: published`, sem nenhum step
adicional aguardando o `ci.yml`.

**Rationale**: uma release só passa a existir depois que o PR de release é mesclado — e esse
merge, pela decisão da seção 8, só ocorre depois que `build`, `test` e `sonar` passarem para
aquele exato conteúdo (branch protection bloqueia qualquer merge, automático ou manual, até os
checks passarem). Como o merge usa a estratégia "criar commit de merge", a árvore do commit
resultante em `main` é idêntica à do PR que acabou de ser validado — não há conteúdo novo,
não-testado, entrando nesse momento. Um segundo gate em `cd.yml` estaria revalidando a mesma
garantia que a branch protection já oferece na única porta de entrada para `main`, sem reduzir
risco adicional.

**Alternatives considered**:
- **Polling do `ci.yml` dentro de `cd.yml`** (tentativa intermediária, revertida): implementada e
  testada localmente, mas redundante com a garantia da seção 8 uma vez que a branch protection
  está configurada — a mesma reversão de complexidade desnecessária se aplica aqui.
- **Trigger `workflow_run` do `ci.yml` em vez de `release: published`**: dispararia `cd.yml` a
  cada conclusão do `ci.yml`, exigindo lógica adicional para filtrar apenas os commits que
  correspondem a uma release publicada (o evento `workflow_run` não carrega esse contexto) —
  mais convoluto do que manter `release: published` como gatilho semântico único.

## Resumo das decisões

| # | Área | Decisão |
|---|---|---|
| 1 | Versionamento/changelog | release-please, modo simple, PR de release |
| 2 | Publicação de imagens | `docker/build-push-action`, 2 targets, `linux/amd64` only |
| 3 | Gatilho do CD | Evento `release: published` do release-please |
| 4a | Ambiente default | `ENV ASPNETCORE_ENVIRONMENT=Production` no stage `runtime` |
| 4b | Connection string ausente | Exception fail-fast em `DependencyInjection.cs` |
| 5 | Compose de release | `docker/docker-compose.release.yml` novo, com `image:` |
| 6 | Smoke test | Service container Postgres + `docker run` do migrator publicado |
| 7 | Constitution | Emenda MINOR (1.0.1 → 1.1.0): stack + exceção de nomes de teste |
| 8 | Auto-merge do PR de release | `gh pr merge --auto --merge`, condicionado à branch protection de `main` |
| 9 | CD sem gate próprio | Desnecessário — branch protection já garante que só entra em `main` o que passou no CI |

Nenhum `NEEDS CLARIFICATION` remanescente.
