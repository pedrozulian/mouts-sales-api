# Quickstart: Cancelar Item da Venda

Guia para validar manualmente o endpoint `DELETE /api/sales/{id}/items/{itemId}` de ponta a
ponta, após a implementação (tasks geradas por `/speckit-tasks`).

## Pré-requisitos

- Ambiente no ar conforme o [README](../../README.md):
  `docker compose -f docker/docker-compose.yml up -d` (Api + PostgreSQL).
- Nenhuma migration nova é necessária para esta feature (ver `research.md`, seção 8).

## Cenário 1 — Cancelar um item sem esgotar os demais

Registre uma venda com dois itens (ver
[quickstart do UC-01](../002-registrar-venda/quickstart.md), Cenário 1) e capture o `id` da venda
e o `id` de um dos itens retornados no corpo (`201 Created`):

```bash
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 1, "unitPrice": 250.00 },
      { "product": { "id": "7e410000-0000-0000-0000-000000000004", "name": "Mousepad XL" }, "quantity": 2, "unitPrice": 49.90 }
    ]
  }'
```

Cancele apenas o item do teclado:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/{itemId}
```

**Esperado**: `204 No Content`, sem corpo.

Confirme consultando a venda em seguida — ela permanece ativa (`isCancelled: false`), o item do
teclado aparece com `isCancelled: true`, o do mousepad continua ativo, e `totalAmount` reflete
apenas o item ainda ativo (`99.80`):

```bash
curl -i http://localhost:8080/api/sales/{id}
```

## Cenário 2 — Cancelar o último item ativo encerra a venda

Repita o Cenário 1 com uma venda de um único item, ou cancele os dois itens em sequência.
Ao cancelar o último item ainda ativo:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/{itemId}
```

**Esperado**: `204 No Content`, sem corpo. Ao consultar a venda em seguida, ela aparece marcada
como cancelada (`isCancelled: true`), com todos os itens cancelados e `totalAmount: 0.00` — mesmo
efeito final de `DELETE /api/sales/{id}` (006), alcançado em uma única requisição de item.

## Cenário 3 — Cancelar item de venda inexistente

```bash
curl -i -X DELETE http://localhost:8080/api/sales/00000000-0000-0000-0000-000000000000/items/00000000-0000-0000-0000-000000000000
```

**Esperado**: `404 Not Found`, corpo com `errors[0].key = "id"`.

## Cenário 4 — Cancelar item inexistente (ou de outra venda)

Usando o `id` de uma venda válida com um `itemId` que não existe ou pertence a outra venda:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/00000000-0000-0000-0000-000000000000
```

**Esperado**: `404 Not Found`, corpo com `errors[0].key = "itemId"`.

## Cenário 5 — Cancelar item de venda já cancelada

Contra uma venda já cancelada (Cenário 2, ou via `DELETE /api/sales/{id}`):

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/{itemId}
```

**Esperado**: `400 Bad Request`, corpo com `errors[0].key = "sale"`.

## Cenário 6 — Cancelar item já cancelado

Repita o `DELETE` do Cenário 1 contra o mesmo item, já cancelado:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/{itemId}
```

**Esperado**: `400 Bad Request`, corpo com `errors[0].key = "item"`.

## Cenário 7 — Duas requisições de cancelamento concorrentes para o mesmo item (FR-015)

Este cenário é sensível a timing e é melhor validado pelos testes automatizados
(`CancelSaleItemConcurrencyTests`, ver abaixo), mas pode ser aproximado manualmente disparando
duas requisições em paralelo contra o mesmo item ativo:

```bash
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/{itemId} &
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/{itemId} &
wait
```

**Esperado**: uma das duas respostas é `204 No Content`; a outra é `400 Bad Request` com
`errors[0].key = "item"` — nunca duas respostas de sucesso, nunca um `500`.

## Validação automatizada equivalente

Os mesmos cenários são cobertos por:

- `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs` — `Sale.CancelItem()`: item ativo entre outros
  ativos, item já cancelado individualmente permanecendo inalterado, venda já cancelada, item
  inexistente, item já cancelado, cascata ao cancelar o último item ativo, emissão de
  `ItemCancelled` e de `SaleCancelled` quando a cascata se aplica.
- `tests/SalesApi.Application.Tests/Sales/CancelSaleItemCommandHandlerTests.cs` — orquestração do
  caso de uso: item encontrado e cancelado, venda não encontrada, item não encontrado, venda já
  cancelada, item já cancelado, conflito de concorrência traduzido para `400`.
- `tests/SalesApi.Api.Tests/Sales/CancelSaleItemEndpointTests.cs` — ponta a ponta via
  `WebApplicationFactory`, incluindo a tradução para `204`/`400`/`404`, requer PostgreSQL local
  ativo (mesmo pré-requisito de `AppDbContextConnectionTests`).
- `tests/SalesApi.Api.Tests/Sales/CancelSaleItemConcurrencyTests.cs` — duas requisições `DELETE`
  concorrentes para o mesmo item via `Task.WhenAll` (mesmo padrão de
  `CancelSaleConcurrencyTests.cs`, 006), verificando exatamente uma `204` e uma `400` (FR-015).
