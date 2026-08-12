# Contrato: Erro Unificado

Evolução transversal do contrato de erro já usado por todos os endpoints existentes
(`POST/PUT/DELETE /api/sales*`) — não introduz endpoint novo. Cobre a lacuna identificada: falhas
inesperadas hoje escapam desse contrato (US3, FR-013 a FR-016).

## Formato (sem mudança)

```json
{
  "errors": [
    { "key": "<chave>", "message": "<mensagem>" }
  ]
}
```

Já usado por toda rejeição de regra de negócio (`400`) e recurso não encontrado (`404`) desde a
feature 002. Este contrato **não muda de forma** — apenas passa a cobrir mais uma origem de
erro.

## Nova cobertura: falha inesperada

**Status**: `500 Internal Server Error`

```json
{
  "errors": [
    { "key": "server", "message": "Ocorreu um erro inesperado. Tente novamente mais tarde." }
  ]
}
```

### Regras

- Esta resposta é produzida por qualquer exceção não tratada por nenhum handler de caso de uso —
  por exemplo, indisponibilidade momentânea do banco de dados, ou qualquer falha de
  infraestrutura que os `Result`/`Notification` do domínio e da aplicação não modelam
  explicitamente (FR-013).
- O corpo MUST NOT conter rastro de pilha, nome de tipo de exceção, mensagem original da exceção
  nem qualquer outro detalhe de implementação, em nenhum ambiente de execução — inclusive
  `Development` (FR-014). Isso é uma mudança deliberada de comportamento: hoje, em
  `Development` (padrão do `docker-compose.yml`), a página de exceção do ASP.NET Core expõe o
  stack trace completo no corpo da resposta.
- A causa original MUST ser registrada em log estruturado, correlacionável à requisição de
  origem pelo mesmo `X-Correlation-Id` já propagado pelo middleware existente — apenas não é
  exposta ao chamador (FR-015).
- Toda resposta de erro já existente para rejeição de regra de negócio (`400`) e recurso não
  encontrado (`404`) permanece exatamente com o mesmo formato e significado — esta feature não
  reclassifica nenhum erro de negócio existente como falha inesperada, nem o contrário (FR-016).

### Exemplo — antes e depois desta feature, mesma falha de infraestrutura

Antes (comportamento atual, corrigido por esta feature):

```http
HTTP/1.1 500 Internal Server Error
Content-Type: text/plain

Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes...
   at Microsoft.EntityFrameworkCore.Update...
```

Depois:

```http
HTTP/1.1 500 Internal Server Error
Content-Type: application/json

{
  "errors": [
    { "key": "server", "message": "Ocorreu um erro inesperado. Tente novamente mais tarde." }
  ]
}
```
