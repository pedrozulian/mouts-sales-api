# Quickstart: Alterar Venda

Guia para validar manualmente o endpoint `PUT /api/sales/{id}` de ponta a ponta, após a
implementação (tasks geradas por `/speckit-tasks`).

## Pré-requisitos

- Ambiente no ar conforme o [README](../../README.md):
  `docker compose -f docker/docker-compose.yml up -d` (Api + PostgreSQL).
- Migração do banco aplicada:
  `dotnet ef database update --project src/SalesApi.Infrastructure --startup-project src/SalesApi.Api`
  (nenhuma migration nova é criada por esta feature — ver `research.md`, seção 6).

## Cenário 1 — Atualizar, adicionar e cancelar itens em uma única requisição

Registre uma venda com dois itens (ver
[quickstart do UC-01](../002-registrar-venda/quickstart.md), Cenário 1) e capture o `id` da
venda e o `id` de cada item retornado no corpo (`201 Created`):

```bash
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 10, "unitPrice": 250.00 },
      { "product": { "id": "7e410000-0000-0000-0000-000000000004", "name": "Mousepad XL" }, "quantity": 2, "unitPrice": 49.90 }
    ]
  }'
```

Em seguida, altere a venda: aumente a quantidade do primeiro item (referenciando seu `id`),
adicione um item novo sem `id`, e omita o segundo item — ele sai cancelado implicitamente:

```bash
curl -i -X PUT http://localhost:8080/api/sales/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "saleDate": "2026-08-09T14:30:00Z",
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "id": "{id-do-item-k68}", "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 12, "unitPrice": 250.00 },
      { "product": { "id": "aa110000-0000-0000-0000-000000000008", "name": "Headset Gamer H5" }, "quantity": 3, "unitPrice": 180.00 }
    ]
  }'
```

**Esperado**: `200 OK`. O item `K68` aparece com `quantity: 12` e desconto recalculado, o
`Headset Gamer H5` aparece como novo item ativo, o `Mousepad XL` aparece com
`isCancelled: true` e fora do `totalAmount` (ver contrato:
[contracts/update-sale.md](contracts/update-sale.md)).

Confirme consultando a venda em seguida:

```bash
curl -i http://localhost:8080/api/sales/{id}
```

## Cenário 2 — Alterar uma venda inexistente

```bash
curl -i -X PUT http://localhost:8080/api/sales/00000000-0000-0000-0000-000000000000 \
  -H "Content-Type: application/json" \
  -d '{
    "saleDate": "2026-08-09T14:30:00Z",
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 2, "unitPrice": 250.00 }
    ]
  }'
```

**Esperado**: `404 Not Found`, corpo com `errors[0].key = "id"`.

## Cenário 3 — Tentar alterar uma venda já cancelada

Como o cancelamento de venda (UC-05) ainda não está implementado nesta API, este cenário só é
validável hoje pelos testes automatizados, que preparam o estado cancelado diretamente no banco
(mesmo padrão descrito em `specs/003-consultar-venda/research.md`, seção 6). Assim que UC-05
(`DELETE /api/sales/{id}`) for entregue, este cenário passa a ser executável manualmente:

1. Registrar uma venda (Cenário 1).
2. Cancelar a venda (endpoint de feature futura).
3. Repetir o `PUT` do Cenário 1 contra essa venda.

**Esperado**: `400 Bad Request`, corpo com `errors[0].key = "sale"`.

## Cenário 4 — Violações de regra de negócio

```bash
# Corpo sem nenhum item
curl -i -X PUT http://localhost:8080/api/sales/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "saleDate": "2026-08-09T14:30:00Z",
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": []
  }'
```

**Esperado**: `400 Bad Request`, `errors[0].key = "items"`.

Outras variações (data ausente, `id` de item inexistente na venda, produto alterado em um item
existente, quantidade fora de 1–20, produto duplicado no corpo) seguem a mesma forma — ver a
tabela completa em [contracts/update-sale.md](contracts/update-sale.md).

## Validação automatizada equivalente

Os mesmos cenários são cobertos por:

- `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs` — `Sale.Update`: reconciliação (atualizar,
  adicionar, cancelar implícito), cada invariante e a regra de imutabilidade de produto.
- `tests/SalesApi.Application.Tests/Sales/UpdateSaleCommandHandlerTests.cs` — orquestração do
  caso de uso: venda encontrada e alterada, venda não encontrada, cada caminho de rejeição.
- `tests/SalesApi.Api.Tests/Sales/UpdateSaleEndpointTests.cs` — ponta a ponta via
  `WebApplicationFactory`, incluindo a tradução para `200`/`400`/`404`, requer PostgreSQL local
  ativo (mesmo pré-requisito de `AppDbContextConnectionTests`).
