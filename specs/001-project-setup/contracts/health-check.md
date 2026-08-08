# Contrato: Health Check

Único endpoint exposto por esta spec (FR-004, User Story 4). Usado para provar, de ponta a
ponta, que a fundação técnica está funcional antes de qualquer endpoint de negócio existir.

## `GET /health`

**Descrição**: reporta o status da aplicação e de suas dependências críticas (nesta fase,
apenas o PostgreSQL).

**Autenticação**: nenhuma — endpoint público, usado por orquestradores/monitoramento.

### Resposta — tudo saudável

**Status**: `200 OK`

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy"
    }
  ]
}
```

### Resposta — dependência indisponível

**Status**: `503 Service Unavailable`

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Unhealthy",
      "description": "<motivo da falha>"
    }
  ]
}
```

### Regras

- O status geral (`status`) MUST refletir o pior status entre as dependências verificadas
  (Edge case da spec: banco indisponível → health check reporta indisponibilidade).
- O endpoint MUST estar listado na documentação interativa (Swagger/OpenAPI), junto com os
  demais endpoints (FR-003).
- Este contrato é o único endpoint funcional entregue por esta spec. Endpoints de negócio
  (CRUD de vendas) pertencem a specs futuras.
