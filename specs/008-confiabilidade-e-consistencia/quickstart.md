# Quickstart: Confiabilidade Operacional e Consistência de Dados

Guia para validar manualmente, de ponta a ponta, as correções desta feature após a implementação
(tasks geradas por `/speckit-tasks`). Diferente das features anteriores, não há um endpoint novo
a testar — os cenários abaixo provam que defeitos hoje confirmados deixaram de existir.

## Pré-requisito comum

Volume de dados do PostgreSQL **limpo** — o próprio Cenário 1 depende disso para ser um teste
real do provisionamento.

```bash
docker compose -f docker/docker-compose.yml down -v
cp docker/.env.example docker/.env   # se ainda não existir
```

## Cenário 1 — Ambiente provisionado com um único comando (US1, FR-001 a FR-005)

```bash
docker compose -f docker/docker-compose.yml up -d
docker compose -f docker/docker-compose.yml ps
```

**Esperado**: os serviços `postgres`, `migrator` e `api` aparecem, com `migrator` no estado
`exited (0)` e `api` em execução — a `api` só inicia depois de `migrator` concluir com sucesso.

```bash
curl -i http://localhost:8080/health
```

**Esperado**: `200 OK`, `{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy"}]}`.

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

**Esperado**: `201 Created` na primeira tentativa, sem nenhum passo manual entre o `up -d` e este
`POST` — contraste direto com o comportamento anterior (`500`, `relation "sale_number_seq" does
not exist`).

```bash
docker compose -f docker/docker-compose.yml up -d   # reexecutar sobre o ambiente já provisionado
```

**Esperado**: `migrator` conclui novamente sem erro (idempotência, FR-004), sem apagar os dados
já gravados.

## Cenário 2 — Diagnóstico correto quando o schema está desatualizado (US1, FR-006)

```bash
docker compose -f docker/docker-compose.yml down -v
docker compose -f docker/docker-compose.yml up -d postgres
docker compose -f docker/docker-compose.yml run --rm --no-deps -p 8080:8080 api
```

Executar a `api` sem o `migrator` ter rodado antes.

```bash
curl -i http://localhost:8080/health
```

**Esperado**: `503 Service Unavailable`, com `checks[0].status = "Unhealthy"` e descrição citando
as migrations pendentes — nunca `200 Healthy` contra um schema inexistente.

## Cenário 3 — Valores monetários exatos e estáveis entre escrita e leitura (US2, FR-008 a FR-012)

Usar quantidade e preço unitário que produzam desconto com mais de duas casas decimais (4
unidades a 10%, preço com centavos):

```bash
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Item A" }, "quantity": 4, "unitPrice": 12.34 },
      { "product": { "id": "7e410000-0000-0000-0000-000000000004", "name": "Item B" }, "quantity": 4, "unitPrice": 12.34 }
    ]
  }'
```

Anotar `id` da venda e, no corpo da resposta, `items[0].discountAmount`, `items[0].totalAmount` e
`totalAmount` da venda.

```bash
curl -i http://localhost:8080/api/sales/{id}
```

**Esperado**: todos os valores monetários da consulta são **idênticos** aos devolvidos pelo
`POST` — sem a divergência anteriormente observada (`4.9360` no `POST` vs. `4.94` no `GET`).
`totalAmount` da venda é exatamente a soma dos dois `totalAmount` de item, sem diferença de
centavo (`88.84`, não `88.85`).

## Cenário 4 — Toda falha responde no mesmo contrato de erro (US3, FR-013 a FR-016)

Requer provocar uma falha de infraestrutura real (por exemplo, interromper o PostgreSQL no meio
de uma requisição) — cenário mais adequado à validação automatizada
(`ErrorHandlingTests`, ver abaixo). Aproximação manual:

```bash
docker compose -f docker/docker-compose.yml stop postgres
curl -i -X POST http://localhost:8080/api/sales \
  -H "Content-Type: application/json" \
  -d '{"customer":{"id":"9f1c8f2a-0000-0000-0000-000000000001","name":"Maria"},"branch":{"id":"3a7d1b04-0000-0000-0000-000000000002","name":"Centro"},"items":[{"product":{"id":"c02b0000-0000-0000-0000-000000000003","name":"Item"},"quantity":1,"unitPrice":10}]}'
docker compose -f docker/docker-compose.yml start postgres
```

**Esperado**: `500 Internal Server Error` com corpo
`{"errors":[{"key":"server","message":"..."}]}` — nunca texto plano, stack trace ou nome de tipo
de exceção no corpo, mesmo com `ASPNETCORE_ENVIRONMENT=Development` (padrão do compose).

## Cenário 5 — Reintroduzir produto de item cancelado é recusado como regra de negócio (US4, FR-017 a FR-019)

```bash
# 1. Registrar venda com dois produtos
curl -s -X POST http://localhost:8080/api/sales -H "Content-Type: application/json" -d '{
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "items": [
    { "product": { "id": "aaaaaaaa-0000-0000-0000-000000000001", "name": "Produto A" }, "quantity": 2, "unitPrice": 10.00 },
    { "product": { "id": "bbbbbbbb-0000-0000-0000-000000000002", "name": "Produto B" }, "quantity": 2, "unitPrice": 10.00 }
  ]
}'
# Anotar {id} da venda e {itemIdA} do Produto A

# 2. Cancelar o item do Produto A
curl -i -X DELETE http://localhost:8080/api/sales/{id}/items/{itemIdA}

# 3. Tentar reintroduzir o Produto A como item novo via PUT
curl -i -X PUT http://localhost:8080/api/sales/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "saleDate": "2026-08-11T10:00:00Z",
    "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
    "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
    "items": [
      { "id": "{itemIdB}", "product": { "id": "bbbbbbbb-0000-0000-0000-000000000002", "name": "Produto B" }, "quantity": 2, "unitPrice": 10.00 },
      { "product": { "id": "aaaaaaaa-0000-0000-0000-000000000001", "name": "Produto A" }, "quantity": 3, "unitPrice": 10.00 }
    ]
  }'
```

**Esperado**: `400 Bad Request` com `errors[0].key` identificando o item do Produto A no corpo
(`items[1].product.id`) — nunca `500` com violação de constraint do banco (comportamento
anterior confirmado: `23505: duplicate key value violates unique constraint`).

## Cenário 6 — Modelo físico em snake_case, sem delimitação especial (US6, FR-020 a FR-023)

```bash
docker exec -it $(docker compose -f docker/docker-compose.yml ps -q postgres) \
  psql -U salesapi -d salesapi -c "SELECT id, sale_number, total_amount FROM sales LIMIT 1;"
docker exec -it $(docker compose -f docker/docker-compose.yml ps -q postgres) \
  psql -U salesapi -d salesapi -c "SELECT id, sale_id, product_id, total_amount FROM sale_items LIMIT 1;"
```

**Esperado**: ambas as consultas funcionam sem aspas em nenhum identificador — incluindo
`sale_id`, que hoje exige `"SaleId"` entre aspas.

## Validação automatizada equivalente

- `tests/SalesApi.Domain.Tests/Sales/SaleItemTests.cs` e `SaleTests.cs` — fronteiras de
  arredondamento (valor exatamente no meio do centavo), total da venda como soma exata dos itens,
  `ValidateChange` reaproveitado.
- `tests/SalesApi.Domain.Tests/Sales/SaleTests.cs` — reintrodução de produto de item cancelado
  rejeitada com `Notification`, sem mutar estado.
- `tests/SalesApi.Api.Tests/HealthCheckTests.cs` — cenário de schema desatualizado reportando
  `Unhealthy`, via banco propositalmente não migrado.
- `tests/SalesApi.Api.Tests/Infrastructure/ErrorHandlingTests.cs` — exceção não tratada responde
  no contrato de erro unificado, sem detalhe interno, em qualquer ambiente.
- `tests/SalesApi.Api.Tests/Sales/UpdateSaleEndpointTests.cs` — reintrodução de produto de item
  cancelado via `PUT` responde `400` ponta a ponta.
- `tests/SalesApi.Application.Tests/Common/MediatorRegistrationTests.cs` e
  `MapsterConfigurationTests.cs` — reescritos contra tipos reais do domínio, sem `PingQuery` nem
  `SampleMapping`.
- Suíte completa (`dotnet test SalesApi.sln --collect:"XPlat Code Coverage"`) — cobertura mantida
  acima de 90%, sem nenhum teste falho, após a remoção de scaffolding.
