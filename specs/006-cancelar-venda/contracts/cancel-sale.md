# Contrato: Cancelar Venda

Único endpoint entregue por esta feature (UC-05, User Stories 1–3).

## `DELETE /api/sales/{id}`

**Descrição**: cancela logicamente uma venda ativa e todos os seus itens ainda ativos, zerando o
total geral da venda. O registro nunca é removido fisicamente e permanece consultável.

**Autenticação**: nenhuma (fora do escopo desta feature — ver `spec.md`, seção Assumptions).

### Parâmetros de rota

| Parâmetro | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `id` | `Guid` | sim | Identificador técnico da venda (`Sale.Id`), retornado no registro (`POST /api/sales`) |

### Corpo da requisição

Nenhum.

### Resposta — sucesso

**Status**: `204 No Content`

Sem corpo. A venda e todos os seus itens ainda ativos no momento da requisição passam a constar
como cancelados; o total geral da venda passa a ser `0.00`. Confirmável em seguida via
`GET /api/sales/{id}` (ver [contracts/get-sale.md](../../003-consultar-venda/contracts/get-sale.md)):

```json
{
  "id": "b6e20000-0000-0000-0000-000000000005",
  "saleNumber": "V-000123",
  "saleDate": "2026-08-09T14:30:00Z",
  "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
  "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
  "totalAmount": 0.00,
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

### Resposta — venda não encontrada (User Story 2)

**Status**: `404 Not Found`

```json
{
  "errors": [
    { "key": "id", "message": "Venda não encontrada." }
  ]
}
```

### Resposta — venda já cancelada, ou conflito de concorrência (User Story 2)

**Status**: `400 Bad Request`

```json
{
  "errors": [
    { "key": "sale", "message": "Venda já está cancelada." }
  ]
}
```

Esta mesma resposta cobre dois cenários indistinguíveis do ponto de vista do chamador: (a) a
venda já estava cancelada antes desta requisição; (b) duas requisições de cancelamento chegaram
quase simultaneamente para a mesma venda ativa — a que perder a corrida recebe exatamente esta
resposta, nunca um erro de servidor (`500`), conforme FR-013 e a sessão de `/speckit-clarify`.

### Regras

- Nenhuma alteração é persistida quando a resposta é `400` ou `404` (FR-010) — a venda consultada
  em seguida (quando existente) permanece exatamente como estava antes da tentativa.
- O cancelamento em massa dos itens não emite um evento de cancelamento por item — apenas um
  único evento de cancelamento da venda é registrado (FR-008/FR-009).
- Itens que já estavam cancelados individualmente antes desta requisição permanecem inalterados
  (FR-012).
- Este contrato cobre apenas o cancelamento da venda inteira. Cancelamento de item individual
  pertence a uma feature futura (`spec.md`, seção Assumptions).
