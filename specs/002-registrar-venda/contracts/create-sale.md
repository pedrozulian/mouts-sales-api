# Contrato: Registrar Venda

Único endpoint entregue por esta feature (UC-01, User Stories 1–3).

## `POST /api/sales`

**Descrição**: registra uma nova venda com um ou mais itens, aplicando desconto por
quantidade e calculando os totais.

**Autenticação**: nenhuma (fora do escopo desta feature — ver `spec.md`, seção Assumptions).

### Requisição

```json
{
  "saleDate": "2026-08-09T14:30:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "items": [
    { "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 10, "unitPrice": 250.00 },
    { "product": { "id": "7e410000-0000-0000-0000-000000000004", "name": "Mousepad XL" }, "quantity": 2, "unitPrice": 49.90 }
  ]
}
```

`saleDate` é opcional (FR-011). Nenhum campo de desconto ou total é aceito — se enviado, é
ignorado (FR-010).

### Resposta — sucesso

**Status**: `201 Created`
**Header**: `Location: /api/sales/{id}`

```json
{
  "id": "b6e20000-0000-0000-0000-000000000005",
  "saleNumber": "V-000123",
  "saleDate": "2026-08-09T14:30:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "totalAmount": 2099.90,
  "isCancelled": false,
  "items": [
    {
      "id": "d1a40000-0000-0000-0000-000000000006",
      "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" },
      "quantity": 10,
      "unitPrice": 250.00,
      "discountPercentage": 0.20,
      "discountAmount": 500.00,
      "totalAmount": 2000.00,
      "isCancelled": false
    },
    {
      "id": "f7b20000-0000-0000-0000-000000000007",
      "product": { "id": "7e410000-0000-0000-0000-000000000004", "name": "Mousepad XL" },
      "quantity": 2,
      "unitPrice": 49.90,
      "discountPercentage": 0,
      "discountAmount": 0,
      "totalAmount": 99.80,
      "isCancelled": false
    }
  ]
}
```

### Resposta — regra de negócio violada

**Status**: `400 Bad Request`

```json
{
  "errors": [
    { "key": "items[0].quantity", "message": "Não é possível vender mais de 20 unidades do mesmo produto." }
  ]
}
```

| Situação | `key` | Regra |
|---|---|---|
| Nenhum item informado | `items` | FR-004 / INV-01 |
| Quantidade fora de 1–20 | `items[i].quantity` | FR-003 / INV-02 |
| Produto duplicado entre itens | `items[i].product.id` | FR-005 / INV-03 |
| Preço unitário ≤ 0 | `items[i].unitPrice` | FR-006 / INV-04 |
| Identidade externa incompleta (cliente/filial/produto) | `customer` / `branch` / `items[i].product` | FR-007 |

### Regras

- Toda a requisição é avaliada e, se qualquer item violar uma regra, **nenhum dado é
  persistido** (FR-014) — a resposta lista todas as violações encontradas, não apenas a
  primeira.
- Em caso de sucesso, um evento `SaleCreated` é publicado (log estruturado) após a
  persistência (FR-015, User Story 3).
- Este contrato cobre apenas a criação. Consulta (`GET /api/sales/{id}`), listagem, alteração
  e cancelamento pertencem a features futuras (`spec.md`, seção Assumptions).
