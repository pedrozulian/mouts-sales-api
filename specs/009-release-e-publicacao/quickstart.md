# Quickstart: Release Automatizado e Publicação de Imagens

Guia para validar manualmente, de ponta a ponta, o que esta feature entrega (tasks geradas por
`/speckit-tasks`). Diferente da 008, parte do comportamento aqui só é observável depois de um
merge real em `main` (o fluxo do release-please e o job de publicação rodam no GitHub Actions) — os
cenários abaixo separam o que é validável localmente do que exige observar a execução no GitHub.

## Cenário 1 — Artefatos rodam sem o código-fonte, banco em outra máquina (US1, FR-001 a FR-004, FR-025 a FR-027)

Simula, localmente, o que um avaliador faz ao dar `docker pull`: usa as imagens já publicadas
(ou, antes da primeira release existir, imagens construídas localmente com os mesmos targets do
Dockerfile — substituir a tag de exemplo pela versão real após a primeira publicação).

```bash
# Build local simulando o artefato publicado (mesmo target usado pelo CD)
docker build -f docker/Dockerfile --target runtime -t mouts-sales-api:smoke .
docker build -f docker/Dockerfile --target migrator -t mouts-sales-api-migrator:smoke .

# Postgres isolado, representando "banco em outro servidor"
docker network create sales-smoke
docker run -d --name sales-db --network sales-smoke \
  -e POSTGRES_DB=salesapi -e POSTGRES_USER=salesapi -e POSTGRES_PASSWORD=salesapi \
  postgres:16

# Preparar a estrutura de dados a partir do artefato migrator, sem SDK .NET na máquina
docker run --rm --network sales-smoke \
  -e ConnectionStrings__DefaultConnection="Host=sales-db;Port=5432;Database=salesapi;Username=salesapi;Password=salesapi" \
  mouts-sales-api-migrator:smoke

# Subir a aplicação apontando para o mesmo banco
docker run -d --name sales-api --network sales-smoke -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=sales-db;Port=5432;Database=salesapi;Username=salesapi;Password=salesapi" \
  mouts-sales-api:smoke

curl -i http://localhost:8080/health
```

**Esperado**: `200 OK`, `{"status":"Healthy",...}` — schema criado pelo migrator corresponde
exatamente ao que a aplicação espera, sem nenhum passo manual além dos três comandos acima, sem
`dotnet`, `git clone` ou SDK instalado na máquina.

```bash
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 4, "unitPrice": 12.34 }
    ]
  }'
```

**Esperado**: `201 Created`. Prova que "banco em outro servidor" (aqui, outro container na mesma
rede, representando um host distinto) funciona sem nenhuma mudança nos artefatos.

```bash
docker rm -f sales-api sales-db && docker network rm sales-smoke
```

## Cenário 2 — Falha imediata e legível quando a connection string não é fornecida (US2, FR-008 a FR-011)

```bash
docker run --rm mouts-sales-api:smoke
```

**Esperado**: o container encerra imediatamente (não fica em execução aguardando conexão), e o
log final identifica claramente que `ConnectionStrings__DefaultConnection` (ou a variável
equivalente) não foi fornecida — nunca uma exceção de conexão Npgsql genérica, e nunca uma
tentativa silenciosa de conectar a `localhost`.

```bash
docker run --rm mouts-sales-api:smoke env | grep ASPNETCORE_ENVIRONMENT
```

**Esperado**: `ASPNETCORE_ENVIRONMENT=Production`, sem precisar de nenhuma variável fornecida na
chamada — confirma o default explícito do Dockerfile (FR-011).

```bash
docker run --rm -e ASPNETCORE_ENVIRONMENT=Development mouts-sales-api:smoke env | grep ASPNETCORE_ENVIRONMENT
```

**Esperado**: `ASPNETCORE_ENVIRONMENT=Development` — confirma que o perfil é sobrescrevível sem
exigir um artefato diferente (FR-012).

## Cenário 3 — `docker-compose.release.yml` sobe o sistema completo a partir de imagens publicadas (US1, FR-025, FR-026)

Requer ao menos uma versão já publicada no Docker Hub (após o primeiro merge do PR de release).

```bash
cp docker/.env.example docker/.env
TAG=latest docker compose -f docker/docker-compose.release.yml up -d
docker compose -f docker/docker-compose.release.yml ps
```

**Esperado**: mesma topologia e ordem do `docker/docker-compose.yml` de desenvolvimento —
`postgres` saudável, `migrator` em `exited (0)`, `api` em execução só depois — mas nenhum dos
serviços `api`/`migrator` executa `docker build`; ambos usam `image:` apontando para o Docker
Hub, confirmável com:

```bash
docker compose -f docker/docker-compose.release.yml config | grep -A1 "image:"
```

```bash
docker compose -f docker/docker-compose.release.yml down -v
```

## Cenário 4 — Ciclo completo de release, observado no GitHub (US3, US4, FR-016 a FR-024)

Não reproduzível localmente em sua totalidade — depende do GitHub Actions. Passo a passo para
validar após o merge desta feature em `main`:

1. Fazer um commit convencional trivial em `main` (ex.: `fix: ajuste de mensagem de log`).
2. **Esperado**: o workflow `CI/CD` (`.github/workflows/ci-cd.yml`) roda `build`/`test`/`sonar` e,
   em seguida, o job `release-please` abre (ou atualiza) um Pull Request de release contendo o
   bump de versão em formato semântico e a seção correspondente do `CHANGELOG.md` gerada
   automaticamente — sem edição manual. O job `publish` não roda nesta execução (nenhuma release
   foi criada ainda).
3. Revisar o PR de release e mesclá-lo.
4. **Esperado**: o workflow `CI/CD` dispara de novo para o commit de merge, roda
   `build`/`test`/`sonar` mais uma vez e, no job `release-please`, cria automaticamente uma tag
   Git e uma GitHub Release com o número de versão determinado pelo PR.
5. **Esperado**: nesta mesma execução — não em um workflow separado, e só depois de
   `build`/`test`/`sonar` terem passado para o commit de merge — o job `publish` dispara
   automaticamente, encadeado por `needs` ao job `release-please` que acabou de criar a release.
6. Acompanhar a execução do job `publish` no GitHub Actions.
7. **Esperado**: o job publica as duas imagens (`pedrozulian/mouts-sales-api` e
   `pedrozulian/mouts-sales-api-migrator`) nas tags da versão e `latest`, executa o smoke test do
   migrator publicado contra um Postgres efêmero do próprio job, e só é marcado como bem-sucedido
   se o smoke test sair com código `0`.
8. Conferir no Docker Hub que ambas as imagens existem sob a mesma tag de versão.

## Cenário 5 — Publicação falha antes de ser considerada concluída, se o artefato não funcionar (US4, FR-022 a FR-024)

Validação por inspeção do workflow (não requer quebrar produção): revisar o job `publish` em
`.github/workflows/ci-cd.yml` e confirmar que o passo de smoke test:

- Roda **depois** do push das duas imagens e **antes** do job ser marcado como concluído com
  sucesso.
- Usa um serviço de Postgres declarado no próprio job (`services:` do GitHub Actions), não uma
  infraestrutura externa persistente.
- Falha o job (`exit code != 0` propagado) caso o container do migrator publicado encerre com
  código de saída diferente de zero.

## Validação automatizada equivalente

- Testes de unidade/integração da validação fail-fast em `SalesApi.Infrastructure` — connection
  string ausente lança exceção com mensagem nomeando a variável esperada, antes de
  `AddDbContext` ser configurado.
- Suíte completa (`dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`) — cobertura mantida
  acima de 90%, sem nenhum teste falho, e sem nenhuma alteração de comportamento das seis
  operações existentes (SC-009), após todas as mudanças desta feature.
- Inspeção estática dos identificadores renomeados (US5): busca por `produtoJaPertenceAVenda`,
  `produtoNovo` e `quantidadeInvalida` na base de código deve retornar zero ocorrências após a
  implementação.
