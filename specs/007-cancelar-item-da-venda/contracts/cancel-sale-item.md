# Contrato: Cancelar Item da Venda

Único endpoint entregue por esta feature (UC-06, User Stories 1–4).

## `DELETE /api/sales/{id}/items/{itemId}`

**Descrição**: cancela logicamente um item ativo de uma venda ativa, recalculando o total geral da
venda a partir dos itens ainda ativos. O item cancelado nunca é removido fisicamente e permanece
visível na consulta da venda. Quando o item cancelado é o último ainda ativo, a venda inteira
também é cancelada automaticamente, na mesma operação.

**Autenticação**: nenhuma (fora do escopo desta feature — ver `spec.md`, seção Assumptions).

### Parâmetros de rota

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `id` | `Guid` | sim | Identificador técnico da venda (`Sale.Id`), retornado no registro (`POST /api/sales`) |
| `itemId` | `Guid` | sim | Identificador técnico do item (`SaleItem.Id`), retornado dentro de `items[]` no registro ou na consulta da venda |

### Corpo da requisição

Nenhum.

### Resposta — sucesso, item cancelado sem esgotar os demais (User Story 1)

**Status**: `204 No Content`

Sem corpo. O item indicado passa a constar como cancelado; os demais itens permanecem
inalterados; o total geral da venda passa a refletir a soma apenas dos itens ainda ativos.
Confirmável em seguida via `GET /api/sales/{id}` (ver
[contracts/get-sale.md](../../003-consultar-venda/contracts/get-sale.md)):

```json
{
  "id": "b6e20000-0000-0000-0000-000000000007",
  "saleNumber": "V-000124",
  "saleDate": "2026-08-11T10:00:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "totalAmount": 99.80,
  "isCancelled": false,
  "items": [
    {
      "id": "d1a40000-0000-0000-0000-000000000008",
      "product": { "id": "c02b0000-0000-0000-0000-000000000003", "name": "Teclado Mecânico K68" },
      "quantity": 1,
      "unitPrice": 250.00,
      "discountPercentage": 0.00,
      "discountAmount": 0.00,
      "totalAmount": 250.00,
      "isCancelled": true
    },
    {
      "id": "d1a40000-0000-0000-0000-000000000009",
      "product": { "id": "7e410000-0000-0000-0000-000000000004", "name": "Mousepad XL" },
      "quantity": 2,
      "unitPrice": 49.90,
      "discountPercentage": 0.00,
      "discountAmount": 0.00,
      "totalAmount": 99.80,
      "isCancelled": false
    }
  ]
}
```

### Resposta — sucesso, último item ativo cancelado (User Story 2)

**Status**: `204 No Content`

Sem corpo. Mesmo formato acima, mas com efeito adicional: a venda inteira também passa a constar
como cancelada (`isCancelled: true`, `totalAmount: 0.00`), sem exigir uma segunda requisição — ver
[contracts/cancel-sale.md](../../006-cancelar-venda/contracts/cancel-sale.md) para o formato
completo do recurso nesse estado.

### Resposta — venda ou item não encontrado (User Story 3)

**Status**: `404 Not Found`

```json
{
  "errors": [
    { "key": "id", "message": "Venda não encontrada." }
  ]
}
```

ou, quando a venda existe mas o item não pertence a ela (identificador inexistente ou de outra
venda):

```json
{
  "errors": [
    { "key": "itemId", "message": "Item não encontrado nesta venda." }
  ]
}
```

### Resposta — venda já cancelada (User Story 3)

**Status**: `400 Bad Request`

```json
{
  "errors": [
    { "key": "sale", "message": "Venda cancelada não pode ter itens cancelados." }
  ]
}
```

### Resposta — item já cancelado, ou conflito de concorrência (User Story 3)

**Status**: `400 Bad Request`

```json
{
  "errors": [
    { "key": "item", "message": "Item já está cancelado." }
  ]
}
```

Esta mesma resposta cobre dois cenários indistinguíveis do ponto de vista do chamador: (a) o item
já estava cancelado antes desta requisição; (b) duas requisições de cancelamento chegaram quase
simultaneamente para o mesmo item ativo — a que perder a corrida recebe exatamente esta resposta,
nunca um erro de servidor (`500`), conforme FR-015.

### Regras

- Nenhuma alteração é persistida quando a resposta é `400` ou `404` (FR-013) — a venda consultada
  em seguida (quando existente) permanece exatamente como estava antes da tentativa.
- Um `ItemCancelled` é sempre emitido por cancelamento de item bem-sucedido; um `SaleCancelled`
  adicional só é emitido quando o item cancelado é o último ativo (FR-011/FR-012).
- Este contrato cobre apenas o cancelamento de um item individual. Cancelamento da venda inteira
  em uma única operação explícita pertence à feature 006
  ([contracts/cancel-sale.md](../../006-cancelar-venda/contracts/cancel-sale.md)).
