# Contrato: Consultar Venda

Único endpoint entregue por esta feature (UC-02, User Stories 1–3).

## `GET /api/sales/{id}`

**Descrição**: retorna a venda completa — cliente, filial, itens (ativos e cancelados),
descontos e totais já calculados — a partir do identificador técnico.

**Autenticação**: nenhuma (fora do escopo desta feature — ver `spec.md`, seção Assumptions).

### Parâmetros de rota

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `id` | `Guid` | sim | Identificador técnico da venda (`Sale.Id`), retornado no registro (`POST /api/sales`) |

### Resposta — sucesso, venda ativa

**Status**: `200 OK`

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

### Resposta — sucesso, venda com item cancelado (User Story 2)

**Status**: `200 OK`

Venda ativa (`isCancelled: false`) com um item cancelado — o item permanece na resposta, mas
sai do `totalAmount` da venda:

```json
{
  "id": "b6e20000-0000-0000-0000-000000000005",
  "saleNumber": "V-000123",
  "saleDate": "2026-08-09T14:30:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "totalAmount": 99.80,
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
      "isCancelled": true
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

### Resposta — sucesso, venda cancelada integralmente (User Story 2)

**Status**: `200 OK`

```json
{
  "id": "b6e20000-0000-0000-0000-000000000005",
  "saleNumber": "V-000123",
  "saleDate": "2026-08-09T14:30:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "totalAmount": 0,
  "isCancelled": true,
  "items": [
    {
      "id": "d1a40000-0000-0000-0000-000000000006",
      "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" },
      "quantity": 10,
      "unitPrice": 250.00,
      "discountPercentage": 0.20,
      "discountAmount": 500.00,
      "totalAmount": 2000.00,
      "isCancelled": true
    }
  ]
}
```

### Resposta — venda não encontrada (User Story 3)

**Status**: `404 Not Found`

```json
{
  "errors": [
    { "key": "id", "message": "Venda não encontrada." }
  ]
}
```

### Regras

- A resposta nunca omite itens cancelados — eles permanecem visíveis, apenas com
  `isCancelled: true` e fora do `totalAmount` da venda (FR-006, INV-06 herdada da
  documentação de domínio).
- Nenhum valor de desconto ou total é recalculado nesta consulta — os valores retornados são
  exatamente os persistidos no momento do registro ou de uma alteração anterior (FR-010).
- Este contrato cobre apenas a consulta unitária. Listagem (`GET /api/sales`), alteração e
  cancelamento pertencem a features futuras (`spec.md`, seção Assumptions).
