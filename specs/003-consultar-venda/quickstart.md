# Quickstart: Consultar Venda

Guia para validar manualmente o endpoint `GET /api/sales/{id}` de ponta a ponta, após a
implementação (tasks geradas por `/speckit-tasks`).

## Pré-requisitos

- Ambiente no ar conforme o [README](../../README.md):
  `docker compose -f docker/docker-compose.yml up -d` (Api + PostgreSQL).
- Migração do banco aplicada:
  `dotnet ef database update --project src/SalesApi.Infrastructure --startup-project src/SalesApi.Api`.

## Cenário 1 — Consultar uma venda existente

Registre uma venda (ver
[quickstart do UC-01](../002-registrar-venda/quickstart.md), Cenário 1) e capture o `id`
retornado no corpo da resposta (`201 Created`):

```bash
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 10, "unitPrice": 250.00 }
    ]
  }'
```

Em seguida, consulte a venda pelo `id` retornado:

```bash
curl -i http://localhost:8080/api/sales/{id}
```

**Esperado**: `200 OK`, corpo idêntico ao retornado no registro — mesmo `saleNumber`,
`totalAmount`, desconto e total do item (ver contrato:
[contracts/get-sale.md](contracts/get-sale.md)).

## Cenário 2 — Consultar uma venda inexistente

```bash
curl -i http://localhost:8080/api/sales/00000000-0000-0000-0000-000000000000
```

**Esperado**: `404 Not Found`, corpo com `errors[0].key = "id"`.

## Cenário 3 — Consultar uma venda cancelada (integral ou por item)

Como o cancelamento (UC-05/UC-06) ainda não está implementado nesta API, este cenário só é
validável hoje pelos testes automatizados, que preparam o estado cancelado diretamente no
banco (ver `research.md`, seção 6). Assim que UC-05 (`DELETE /api/sales/{id}`) e UC-06
(`DELETE /api/sales/{id}/items/{itemId}`) forem entregues, este cenário passa a ser
executável manualmente também:

1. Registrar uma venda (Cenário 1).
2. Cancelar a venda ou um de seus itens (endpoint de features futuras).
3. Repetir a consulta do Cenário 1.

**Esperado**: `200 OK` — a venda continua acessível; `isCancelled` reflete o estado no nível
da venda e de cada item; o `totalAmount` considera apenas itens ainda ativos.

## Validação automatizada equivalente

Os mesmos cenários são cobertos por:

- `tests/SalesApi.Application.Tests/Sales/GetSaleQueryHandlerTests.cs` — orquestração do caso
  de uso: venda encontrada, venda não encontrada, venda com item cancelado, venda cancelada
  integralmente (estado preparado diretamente no banco, ver `research.md` seção 6).
- `tests/SalesApi.Api.Tests/Sales/GetSaleEndpointTests.cs` — ponta a ponta via
  `WebApplicationFactory`, incluindo a tradução para `404`, requer PostgreSQL local ativo
  (mesmo pré-requisito de `AppDbContextConnectionTests`).
