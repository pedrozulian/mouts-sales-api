# mouts-sales-api

## Descrição

API REST para registro e consulta de vendas: CRUD completo, cancelamento lógico (da venda
inteira ou de um item individual) e cálculo automático de desconto progressivo por quantidade.
Cliente, filial e produto são identidades externas (id + nome denormalizado) — o serviço não
mantém cadastro próprio deles.

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
