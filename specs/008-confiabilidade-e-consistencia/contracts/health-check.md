# Contrato: Health Check (atualizado)

Evolução do contrato já documentado em
[`specs/001-project-setup/contracts/health-check.md`](../../001-project-setup/contracts/health-check.md).
O formato da resposta **não muda** — o que muda é o critério usado para decidir o status da
dependência `"postgresql"` (US1, FR-006/FR-007).

## `GET /health`

**Descrição**: reporta o status da aplicação e de suas dependências críticas. A partir desta
feature, a verificação de PostgreSQL passa a considerar não apenas a conectividade, mas também
se a estrutura de dados esperada pela aplicação está presente e atualizada.

**Autenticação**: nenhuma — sem mudança.

### Resposta — schema presente e atualizado

**Status**: `200 OK`

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgresql", "status": "Healthy" }
  ]
}
```

### Resposta — banco inacessível (sem mudança em relação ao contrato anterior)

**Status**: `503 Service Unavailable`

```json
{
  "status": "Unhealthy",
  "checks": [
    { "name": "postgresql", "status": "Unhealthy", "description": "<motivo da falha de conexão>" }
  ]
}
```

### Resposta — banco acessível, schema ausente ou desatualizado (nova cobertura desta feature)

**Status**: `503 Service Unavailable`

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Unhealthy",
      "description": "Existem migrations pendentes: 20260809182629_CreateSales, 20260810015402_AddSalesListIndexes."
    }
  ]
}
```

### Regras

- O nome da dependência (`"postgresql"`) permanece único — esta feature não adiciona uma segunda
  entrada em `checks[]` para o mesmo banco; a verificação de schema substitui/absorve a
  verificação de mera conectividade, que já é implícita em consultar o catálogo de migrations
  (FR-007).
- Este é exatamente o cenário que motivou esta feature: antes da correção, este mesmo estado
  (banco acessível, schema inexistente) respondia `200 Healthy` — porque o check anterior só
  testava `SELECT 1` — enquanto `POST /api/sales` respondia `500` (FR-006, edge case da spec).
- O status geral (`status`) continua refletindo o pior status entre as dependências verificadas,
  sem mudança de regra.
