# Quickstart: Listar Vendas

Guia para validar manualmente o endpoint `GET /api/sales` de ponta a ponta, após a
implementação (tasks geradas por `/speckit-tasks`).

## Pré-requisitos

- Ambiente no ar conforme o [README](../../README.md):
  `docker compose -f docker/docker-compose.yml up -d` (Api + PostgreSQL).
- Migrações do banco aplicadas (inclui a nova `AddSalesListIndexes` desta feature):
  `dotnet ef database update --project src/SalesApi.Infrastructure --startup-project src/SalesApi.Api`.

## Cenário 1 — Listar vendas sem filtros

Registre duas ou mais vendas (ver
[quickstart do UC-01](../002-registrar-venda/quickstart.md), Cenário 1), variando `saleDate`
para observar a ordenação:

```bash
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{
    "saleDate": "2026-08-09T14:30:00Z",
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 10, "unitPrice": 250.00 }
    ]
  }'
```

Liste as vendas:

```bash
curl -i http://localhost:8080/api/sales
```

**Esperado**: `200 OK`, `items` ordenados por `saleDate` decrescente, cada item sem a coleção
`items` (de itens da venda), e `page`/`pageSize`/`totalCount`/`totalPages` preenchidos (ver
contrato: [contracts/list-sales.md](contracts/list-sales.md)).

## Cenário 2 — Paginação explícita

```bash
curl -i "http://localhost:8080/api/sales?page=1&pageSize=1"
```

**Esperado**: `200 OK`, exatamente uma venda em `items`, `pageSize: 1`, `totalPages` igual ao
número total de vendas registradas.

## Cenário 3 — Filtrar por cliente, filial e situação de cancelamento

```bash
curl -i "http://localhost:8080/api/sales?customerId=9f1c8f2a-0000-0000-0000-000000000001"
curl -i "http://localhost:8080/api/sales?branchId=3a7d1b04-0000-0000-0000-000000000002"
curl -i "http://localhost:8080/api/sales?isCancelled=false"
```

**Esperado**: `200 OK` em todos os casos, `items` restrito apenas às vendas que atendem ao
filtro informado. Combine parâmetros na mesma URL para observar o `E` lógico (FR-009).

Como o cancelamento (UC-05/UC-06) ainda não está implementado nesta API, `isCancelled=true` só
retorna resultados hoje se um registro cancelado tiver sido preparado diretamente no banco (ver
`research.md`, seção 6) — assim que UC-05/UC-06 forem entregues, este filtro passa a refletir
cancelamentos feitos via API também.

## Cenário 4 — Lista vazia (filtro sem correspondência ou página além do total)

```bash
curl -i "http://localhost:8080/api/sales?customerId=00000000-0000-0000-0000-000000000000"
curl -i "http://localhost:8080/api/sales?page=9999"
```

**Esperado**: `200 OK` em ambos, `items: []`, `totalCount: 0` (no primeiro caso) ou o total
real de registros mantido com `items` vazio (no segundo), nunca `404`.

## Cenário 5 — Parâmetros inválidos

```bash
curl -i "http://localhost:8080/api/sales?page=0"
curl -i "http://localhost:8080/api/sales?pageSize=101"
curl -i "http://localhost:8080/api/sales?customerId=nao-e-um-guid"
curl -i "http://localhost:8080/api/sales?isCancelled=talvez"
```

**Esperado**: `400 Bad Request` em todos os casos, corpo `{ "errors": [{ "key", "message" }] }`
com a `key` correspondente ao parâmetro inválido (ver tabela de mensagens em
[contracts/list-sales.md](contracts/list-sales.md)).

## Validação automatizada equivalente

Os mesmos cenários são cobertos por:

- `tests/SalesApi.Application.Tests/Sales/ListSalesQueryHandlerTests.cs` — orquestração do caso
  de uso: paginação padrão e explícita, filtros isolados e combinados, `isCancelled` ausente vs.
  informado (estado cancelado preparado diretamente no banco, ver `research.md` seção 6), lista
  vazia por filtro ou por página além do total, cada parâmetro inválido isoladamente e em
  combinação, e o desempate por `Id` para vendas com a mesma `saleDate`.
- `tests/SalesApi.Api.Tests/Sales/ListSalesEndpointTests.cs` — ponta a ponta via
  `WebApplicationFactory`, incluindo a tradução para `400`, requer PostgreSQL local ativo (mesmo
  pré-requisito de `AppDbContextConnectionTests`).
