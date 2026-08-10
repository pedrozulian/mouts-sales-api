# Contrato: Listar Vendas

Único endpoint entregue por esta feature (UC-03, User Stories 1–3).

## `GET /api/sales`

**Descrição**: retorna uma página de vendas em forma resumida (sem `items`), ordenada por
`saleDate` decrescente (desempate por `id`), com filtros opcionais.

**Autenticação**: nenhuma (fora do escopo desta feature — ver `spec.md`, seção Assumptions).

### Parâmetros de query

| Parâmetro | Tipo | Obrigatório | Padrão | Descrição |
|---|---|---|---|---|
| `page` | inteiro (string na query) | não | `1` | Página solicitada. Deve ser ≥ 1 |
| `pageSize` | inteiro (string na query) | não | `20` | Itens por página. Deve estar entre 1 e 100 |
| `customerId` | `Guid` (string na query) | não | — | Filtra pela identidade externa do cliente |
| `branchId` | `Guid` (string na query) | não | — | Filtra pela identidade externa da filial |
| `isCancelled` | `bool` (string na query) | não | — | Filtra pela situação de cancelamento. Ausente = ativas e canceladas juntas |

### Resposta — sucesso, com resultados (User Story 1)

**Status**: `200 OK`

```json
{
  "items": [
    {
      "id": "b6e20000-0000-0000-0000-000000000005",
      "saleNumber": "V-000124",
      "saleDate": "2026-08-09T16:10:00Z",
      "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
      "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
      "totalAmount": 2099.90,
      "isCancelled": false
    },
    {
      "id": "a1b20000-0000-0000-0000-000000000009",
      "saleNumber": "V-000123",
      "saleDate": "2026-08-09T14:30:00Z",
      "customer": { "id": "9f1c8f2a-0000-0000-0000-000000000001", "name": "Maria Souza" },
      "branch": { "id": "3a7d1b04-0000-0000-0000-000000000002", "name": "Filial Centro" },
      "totalAmount": 500.00,
      "isCancelled": false
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1
}
```

Observação: cada item da lista **não** inclui `items` (a coleção de itens da venda) —
deliberadamente, para manter o payload leve (FR-004). Para o detalhe completo de uma venda,
usar `GET /api/sales/{id}` (`003-consultar-venda`).

### Resposta — sucesso, sem resultados (User Story 3)

**Status**: `200 OK`

Retornada tanto quando nenhum registro atende aos filtros quanto quando `page` está além do
total de páginas existentes (FR-010):

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

### Resposta — parâmetro inválido (User Story 3)

**Status**: `400 Bad Request`

Exemplo com múltiplos parâmetros inválidos simultaneamente — todos os erros são retornados de
uma vez, não apenas o primeiro encontrado:

```json
{
  "errors": [
    { "key": "pageSize", "message": "O tamanho de página deve estar entre 1 e 100." },
    { "key": "customerId", "message": "Identificador de cliente em formato inválido." }
  ]
}
```

Outras mensagens possíveis, uma por parâmetro:

| `key` | Condição |
|---|---|
| `page` | não numérico, ou menor que 1 |
| `pageSize` | não numérico, menor que 1, ou maior que 100 |
| `customerId` | informado e não conversível para `Guid` |
| `branchId` | informado e não conversível para `Guid` |
| `isCancelled` | informado e não conversível para `bool` (`true`/`false`) |

### Regras

- A ordenação é sempre por `saleDate` decrescente, com `id` como desempate — garante ordem
  determinística entre páginas mesmo quando duas vendas têm exatamente a mesma data (ver
  `spec.md`, seção Clarifications).
- Múltiplos filtros informados juntos se combinam por `E` lógico (FR-009) — ex.:
  `customerId` + `branchId` retorna apenas vendas que atendem a ambos.
- Nenhum valor de desconto ou total é recalculado nesta consulta — os valores retornados em
  `totalAmount` são exatamente os persistidos (FR-014).
- Nenhum evento de domínio é disparado e nenhum estado é alterado por esta consulta (FR-013,
  FR-015).
- Este contrato cobre apenas a listagem. Consulta unitária (`003-consultar-venda`), alteração e
  cancelamento pertencem a outras features (`spec.md`, seção Assumptions).
