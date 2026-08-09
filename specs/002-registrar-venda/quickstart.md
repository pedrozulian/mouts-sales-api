# Quickstart: Registrar Venda

Guia para validar manualmente o endpoint `POST /api/sales` de ponta a ponta, após a
implementação (tasks geradas por `/speckit-tasks`).

## Pré-requisitos

- Ambiente no ar conforme o [README](../../README.md):
  `docker compose -f docker/docker-compose.yml up -d` (Api + PostgreSQL).
- Migração do banco aplicada (tabelas `sales` e `sale_items` existentes):
  `dotnet ef database update --project src/SalesApi.Infrastructure --startup-project src/SalesApi.Api`.

## Cenário 1 — Venda sem desconto (1 a 3 unidades)

```bash
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 2, "unitPrice": 250.00 }
    ]
  }'
```

**Esperado**: `201 Created`, `discountPercentage` = 0, total do item = 500.00 (ver contrato:
[contracts/create-sale.md](contracts/create-sale.md)).

## Cenário 2 — Venda com desconto de 10% (4 a 9 unidades)

Repita com `"quantity": 5`. **Esperado**: `discountPercentage` = 0.10, total do item = 1125.00
(1250 bruto × 0,9).

## Cenário 3 — Venda com desconto de 20% (10 a 20 unidades)

Repita com `"quantity": 15`. **Esperado**: `discountPercentage` = 0.20, total do item = 3000.00
(3750 bruto × 0,8).

## Cenário 4 — Rejeição por limite de quantidade (acima de 20)

Repita com `"quantity": 21`. **Esperado**: `400 Bad Request`, corpo com
`errors[0].key = "items[0].quantity"`. Confirme, com uma consulta posterior ao banco, que
nenhuma linha foi inserida em `sales`.

## Cenário 5 — Rejeição por produto duplicado

Envie dois itens com o mesmo `product.id`. **Esperado**: `400 Bad Request` citando o produto
duplicado (`items[i].product.id`).

## Cenário 6 — Evento de domínio

Após o Cenário 1, inspecione o log estruturado da Api (`docker compose logs api` ou o console
de `dotnet run`). **Esperado**: uma entrada de log correspondente ao evento `SaleCreated`,
contendo o `SaleId` e o `SaleNumber` retornados na resposta.

## Validação automatizada equivalente

Os mesmos cenários são cobertos por:

- `tests/SalesApi.Domain.Tests/Sales/DiscountPolicyTests.cs` e `SaleTests.cs` — regras de
  desconto e invariantes, unitário.
- `tests/SalesApi.Application.Tests/Sales/CreateSaleCommandHandlerTests.cs` — orquestração do
  caso de uso.
- `tests/SalesApi.Api.Tests/Sales/CreateSaleEndpointTests.cs` — ponta a ponta via
  `WebApplicationFactory`, requer PostgreSQL local ativo (mesmo pré-requisito de
  `AppDbContextConnectionTests`).
