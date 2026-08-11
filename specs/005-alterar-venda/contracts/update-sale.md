# Contrato: Alterar Venda

Único endpoint entregue por esta feature (UC-04, User Stories 1–3).

## `PUT /api/sales/{id}`

**Descrição**: substitui o cabeçalho (cliente, filial, data) de uma venda ativa e reconcilia
seus itens com o corpo da requisição — item com `id` conhecido é atualizado, item sem
`id` é adicionado, item ausente do corpo é cancelado logicamente.

**Autenticação**: nenhuma (fora do escopo desta feature — ver `spec.md`, seção Assumptions).

### Parâmetros de rota

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `id` | `Guid` | sim | Identificador técnico da venda (`Sale.Id`), retornado no registro (`POST /api/sales`) |

### Corpo da requisição

```json
{
  "saleDate": "2026-08-09T14:30:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch":   { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "items": [
    { "id": "d1a40000-0000-0000-0000-000000000006", "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" }, "quantity": 12, "unitPrice": 250.00 },
    { "product": { "id": "aa110000-0000-0000-0000-000000000008", "name": "Headset Gamer H5" }, "quantity": 3, "unitPrice": 180.00 }
  ]
}
```

`saleDate`, `customer` e `branch` são obrigatórios (diferente do registro, onde a data é
opcional — FR-002). Cada item do array pode trazer `id` (atualiza um item existente, mesmo
identificador retornado por `GET /api/sales/{id}`) ou omiti-lo (adiciona um item novo). Todo
item ativo da venda cujo `id` não aparecer neste array é cancelado implicitamente. Desconto e
totais não são aceitos no payload (FR-013); se enviados, são ignorados.

### Resposta — sucesso, reconciliação completa

**Status**: `200 OK`

Exemplo: venda com três itens ativos (`K68`, `Mousepad XL`, `Fone P2`) recebe um pedido que
atualiza a quantidade do `K68`, adiciona um `Headset Gamer H5` e omite o `Mousepad XL` e o
`Fone P2` — ambos saem cancelados implicitamente:

```json
{
  "id": "b6e20000-0000-0000-0000-000000000005",
  "saleNumber": "V-000123",
  "saleDate": "2026-08-09T14:30:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "totalAmount": 2940.00,
  "isCancelled": false,
  "items": [
    {
      "id": "d1a40000-0000-0000-0000-000000000006",
      "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" },
      "quantity": 12,
      "unitPrice": 250.00,
      "discountPercentage": 0.20,
      "discountAmount": 600.00,
      "totalAmount": 2400.00,
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
      "isCancelled": true
    },
    {
      "id": "8c930000-0000-0000-0000-000000000009",
      "product": { "id": "5d220000-0000-0000-0000-00000000000a", "name": "Fone P2" },
      "quantity": 1,
      "unitPrice": 29.90,
      "discountPercentage": 0,
      "discountAmount": 0,
      "totalAmount": 29.90,
      "isCancelled": true
    },
    {
      "id": "1e440000-0000-0000-0000-00000000000b",
      "product": { "id": "aa110000-0000-0000-0000-000000000008", "name": "Headset Gamer H5" },
      "quantity": 3,
      "unitPrice": 180.00,
      "discountPercentage": 0,
      "discountAmount": 0,
      "totalAmount": 540.00,
      "isCancelled": false
    }
  ]
}
```

Note que `totalAmount` da venda (`2400.00 + 540.00 = 2940.00`, calculado apenas sobre os itens
ativos) reflete exclusivamente `K68` e `Headset Gamer H5` — os dois itens cancelados permanecem
visíveis, com `isCancelled: true`, mas fora do total.

### Resposta — venda não encontrada (User Story 2)

**Status**: `404 Not Found`

```json
{
  "errors": [
    { "key": "id", "message": "Venda não encontrada." }
  ]
}
```

### Resposta — venda já cancelada (User Story 2)

**Status**: `400 Bad Request`

```json
{
  "errors": [
    { "key": "sale", "message": "Venda cancelada não pode ser alterada." }
  ]
}
```

### Resposta — demais violações de regra de negócio (User Story 2)

**Status**: `400 Bad Request`

```json
{
  "errors": [
    { "key": "items", "message": "A venda deve ter ao menos um item." }
  ]
}
```

Outras violações seguem a mesma forma, variando apenas `key`/`message`:

| Condição | `key` | Mensagem |
|---|---|---|
| Data ausente no corpo | `saleDate` | "Data da venda é obrigatória." |
| `id` que não pertence à venda | `items[{i}].id` | "Item não pertence a esta venda." |
| `id` de item já cancelado | `items[{i}].id` | "Item já está cancelado e não pode ser alterado." |
| Produto de item existente alterado | `items[{i}].product.id` | "Produto de um item existente não pode ser alterado." |
| Quantidade fora de 1–20 | `items[{i}].quantity` | "Não é possível vender mais de 20 unidades do mesmo produto." / "A quantidade deve ser de ao menos 1 unidade." |
| Produto duplicado no corpo | `items[{i}].product.id` | "Produto duplicado entre os itens da venda." |

### Regras

- Nenhuma alteração é persistida quando a resposta é `400` ou `404` (FR-017) — a venda
  consultada em seguida permanece exatamente como estava antes da tentativa.
- A resposta de sucesso usa exatamente o mesmo formato de `GET /api/sales/{id}` (FR-014) — ver
  [contracts/get-sale.md](../../003-consultar-venda/contracts/get-sale.md).
- Este contrato cobre apenas a alteração via substituição completa (`PUT`). Cancelamento total
  da venda e cancelamento isolado de um item pertencem a features futuras (`spec.md`, seção
  Assumptions).
