# Quickstart: Cancelar Venda

Guia para validar manualmente o endpoint `DELETE /api/sales/{id}` de ponta a ponta, após a
implementação (tasks geradas por `/speckit-tasks`).

## Pré-requisitos

- Ambiente no ar conforme o [README](../../README.md):
  `docker compose -f docker/docker-compose.yml up -d` (Api + PostgreSQL).
- Nenhuma migration nova é necessária para esta feature (ver `research.md`, seção 9).

## Cenário 1 — Cancelar uma venda ativa

Registre uma venda com um item (ver
[quickstart do UC-01](../002-registrar-venda/quickstart.md), Cenário 1) e capture o `id` da venda
retornado no corpo (`201 Created`):

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

Cancele a venda:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}
```

**Esperado**: `204 No Content`, sem corpo.

Confirme consultando a venda em seguida — ela permanece acessível, com `isCancelled: true` e
`totalAmount: 0.00`:

```bash
curl -i http://localhost:8080/api/sales/{id}
```

## Cenário 2 — Cancelar uma venda inexistente

```bash
curl -i -X DELETE http://localhost:8080/api/sales/00000000-0000-0000-0000-000000000000
```

**Esperado**: `404 Not Found`, corpo com `errors[0].key = "id"`.

## Cenário 3 — Cancelar uma venda já cancelada

Repita o `DELETE` do Cenário 1 contra a mesma venda, já cancelada:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}
```

**Esperado**: `400 Bad Request`, corpo com `errors[0].key = "sale"` e mensagem "Venda já está
cancelada.".

## Cenário 4 — Duas requisições de cancelamento concorrentes (FR-013)

Este cenário é sensível a timing e é melhor validado pelos testes automatizados
(`CancelSaleConcurrencyTests`, ver abaixo), mas pode ser aproximado manualmente disparando duas
requisições em paralelo contra a mesma venda ativa:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id} &
curl -i -X DELETE http://localhost:8080/api/sales/{id} &
wait
```

**Esperado**: uma das duas respostas é `204 No Content`; a outra é `400 Bad Request` com
`errors[0].key = "sale"` — nunca duas respostas de sucesso, nunca um `500`.

## Validação automatizada equivalente

Os mesmos cenários são cobertos por:

- `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs` — `Sale.Cancel()`: venda com itens ativos,
  venda já cancelada, itens já cancelados individualmente permanecendo inalterados, emissão de
  `SaleCancelled`.
- `tests/SalesApi.Application.Tests/Sales/CancelSaleCommandHandlerTests.cs` — orquestração do
  caso de uso: venda encontrada e cancelada, venda não encontrada, venda já cancelada, conflito
  de concorrência traduzido para `400`.
- `tests/SalesApi.Api.Tests/Sales/CancelSaleEndpointTests.cs` — ponta a ponta via
  `WebApplicationFactory`, incluindo a tradução para `204`/`400`/`404`, requer PostgreSQL local
  ativo (mesmo pré-requisito de `AppDbContextConnectionTests`).
- `tests/SalesApi.Api.Tests/Sales/CancelSaleConcurrencyTests.cs` — duas requisições `DELETE`
  concorrentes para a mesma venda via `Task.WhenAll` (mesmo padrão de
  `CreateSaleConcurrencyTests.cs`), verificando exatamente uma `204` e uma `400` (FR-013).
