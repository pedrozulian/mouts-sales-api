# mouts-sales-api

## Descrição

API REST para registro e consulta de vendas: CRUD completo, cancelamento lógico (da venda
inteira ou de um item individual) e cálculo automático de desconto progressivo por quantidade.
Cliente, filial e produto são identidades externas (id + nome denormalizado) — o serviço não
mantém cadastro próprio deles.

## Documentação de domínio (DDD)

O modelo de domínio, os bounded contexts, as invariantes e os casos de uso que embasaram esta
implementação estão documentados em
[Mouts — Sales API (Notion)](https://harmonious-chiller-30d.notion.site/Mouts-Sales-API-3b7bddf5f1a0819a9483d3ffde0a6186?pvs=74).
A página inicial já cobre propósito, escopo, arquitetura e padrões adotados; as três páginas
vinculadas a ela aprofundam bounded contexts, modelo de domínio e casos de uso.

## Tecnologias

- .NET 8 / C# 12 — ASP.NET Core (Minimal APIs)
- PostgreSQL 16 + Entity Framework Core
- MediatR (casos de uso e eventos de domínio)
- Mapster (mapeamento de objetos)
- Swagger / OpenAPI
- Serilog (logging estruturado)
- xUnit + Testcontainers
- Docker / Docker Compose
- GitHub Actions + SonarCloud (CI e qualidade de código)

## Pré-requisitos

- [Docker](https://docs.docker.com/get-docker/) e Docker Compose
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — opcional, só para rodar
  comandos `dotnet` fora do container

## Instalação e execução

```bash
cp docker/.env.example docker/.env
docker compose -f docker/docker-compose.yml up -d
```

Isso sobe, em ordem, três serviços: `postgres` (banco de dados), `migrator` (aplica as
migrations pendentes e termina) e `api` (só inicia depois que o `migrator` conclui com sucesso).
A API fica disponível em `http://localhost:8080`.

Para rodar localmente fora do Docker (ex.: hot reload durante o desenvolvimento), com o
PostgreSQL do passo acima no ar:

```bash
dotnet ef database update --project src/SalesApi.Infrastructure --startup-project src/SalesApi.Api
dotnet run --project src/SalesApi.Api
```

## Execução a partir de imagens publicadas

Alternativa à seção anterior para quem quer rodar a API sem clonar o repositório nem compilar
nada localmente — usando as imagens publicadas no Docker Hub
([`pedrozulian/mouts-sales-api`](https://hub.docker.com/r/pedrozulian/mouts-sales-api) e
[`pedrozulian/mouts-sales-api-migrator`](https://hub.docker.com/r/pedrozulian/mouts-sales-api-migrator)),
contra um PostgreSQL à sua escolha — na mesma máquina, em outro container ou em outro servidor.

Com Docker Compose, contra o PostgreSQL provisionado pelo próprio compose:

```bash
cp docker/.env.example docker/.env
TAG=latest docker compose -f docker/docker-compose.release.yml up -d
```

Mesma ordem de provisionamento da seção anterior (`postgres` → `migrator` → `api`), mas nenhum
dos dois serviços da aplicação é construído localmente — ambos usam `image:` apontando para o
Docker Hub. Use `TAG=<versão>` para fixar uma versão específica em vez de `latest` (ver
[Releases do GitHub](https://github.com/pedrozulian/mouts-sales-api/releases) e
[`CHANGELOG.md`](CHANGELOG.md) para as versões disponíveis).

Sem Compose, apontando para um banco de dados em outro servidor:

```bash
# 1. Preparar a estrutura de dados (uma vez, ou a cada nova versão)
docker run --rm \
  -e ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<senha>" \
  pedrozulian/mouts-sales-api-migrator:latest

# 2. Subir a aplicação apontando para o mesmo banco
docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<senha>" \
  pedrozulian/mouts-sales-api:latest
```

`ConnectionStrings__DefaultConnection` é a única variável obrigatória — sem ela, o container
encerra imediatamente na inicialização com uma mensagem indicando o que falta, em vez de subir e
falhar de forma obscura na primeira requisição. Por default a imagem assume
`ASPNETCORE_ENVIRONMENT=Production`; para rodar em modo desenvolvimento, defina essa variável
explicitamente — não é necessária uma imagem diferente para isso.

## Uso

- **Documentação interativa**: `http://localhost:8080/swagger`
- **Health check**: `http://localhost:8080/health`

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/sales` | Registrar venda |
| `GET` | `/api/sales/{id}` | Consultar venda |
| `GET` | `/api/sales` | Listar vendas (paginado) |
| `PUT` | `/api/sales/{id}` | Alterar venda |
| `DELETE` | `/api/sales/{id}` | Cancelar venda |
| `DELETE` | `/api/sales/{id}/items/{itemId}` | Cancelar item da venda |

## Testes

```bash
dotnet test SalesApi.sln --collect:"XPlat Code Coverage"
```

Os testes de integração sobem seu próprio PostgreSQL efêmero via Testcontainers — não dependem
do ambiente subido no passo de instalação. Cobertura mínima de 90%, verificada no CI via
SonarCloud.

## Qualidade de código (SonarQube local)

```bash
docker compose -f docker/docker-compose.yml up -d sonarqube
```

Disponível em `http://localhost:9000` (login inicial `admin`/`admin`, troca de senha exigida no
primeiro acesso).

1. Gere um token em **My Account → Security**.
2. Instale o SonarScanner for .NET:

   ```bash
   dotnet tool install --global dotnet-sonarscanner
   ```

3. Rode a análise (o `/s:` exige caminho absoluto):

   ```bash
   dotnet sonarscanner begin /k:"mouts-sales-api" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="<seu-token>" /s:"$(pwd)/SonarQube.Analysis.xml"
   dotnet build SalesApi.sln
   dotnet test SalesApi.sln --collect:"XPlat Code Coverage" --results-directory ./coverage
   dotnet sonarscanner end /d:sonar.token="<seu-token>"
   ```

4. Resultado em `http://localhost:9000`.

---

Documentação técnica detalhada (arquitetura, decisões de design, contratos de API) em
[`specs/`](specs/).
