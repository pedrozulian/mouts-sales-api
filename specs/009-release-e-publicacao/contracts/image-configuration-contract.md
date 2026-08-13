# Contrato: Configuração dos Artefatos Publicados

**Feature**: `009-release-e-publicacao`

Contrato entre os artefatos executáveis publicados e quem os executa — o que cada um exige,
aceita e garante, independentemente do ambiente em que rodam. Cobre FR-008 a FR-015.

## Imagem da aplicação

**Entrada obrigatória**:

| Variável | Formato | Comportamento se ausente |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | connection string Npgsql (`Host=...;Port=...;Database=...;Username=...;Password=...`) | Processo encerra na inicialização com mensagem nomeando a variável ausente; não fica no ar |

**Entrada opcional**:

| Variável | Valores aceitos | Default |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production`, `Development`, ou qualquer nome de ambiente customizado | `Production` |

**Garantias**:

- O mesmo binário roda sob qualquer valor de configuração — nenhuma informação de ambiente é
  compilada dentro da imagem.
- Na ausência de `ConnectionStrings__DefaultConnection`, a aplicação **nunca** tenta conectar a
  um endereço de banco predefinido (ex.: `localhost`) — apenas falha, de forma nomeada.
- `/health` reflete o estado real de conectividade e de aderência do schema, independentemente
  do valor de `ASPNETCORE_ENVIRONMENT`.
- Porta de escuta: `8080` (`ASPNETCORE_URLS=http://+:8080`), inalterada por esta feature.

**Fora do contrato** (comportamento não garantido/não coberto): TLS terminação, autenticação,
rate limiting — seguem fora de escopo do projeto como um todo.

## Imagem do migrator

**Entrada** (uma das duas, precedência do argumento sobre a variável):

| Origem | Formato |
|---|---|
| `ConnectionStrings__DefaultConnection` (env var) | Igual à da aplicação |
| `--connection "<string>"` (argumento de execução) | Connection string completa; sobrepõe a env var quando ambos fornecidos |

**Garantias**:

- Execução é única e finita — o processo sempre encerra, nunca fica residente.
- Código de saída `0` significa exclusivamente que a estrutura de dados foi aplicada com sucesso
  (schema já atualizado ou migrations aplicadas nesta execução). Qualquer outra condição —
  conexão recusada, connection string ausente, falha de aplicação de migration — encerra com
  código de saída diferente de zero.
- Execução idempotente: repetir a execução contra um banco já atualizado é um no-op que também
  encerra com código `0`.
- Não requer nenhuma ferramenta além do runtime de contêiner — é self-contained, sem dependência
  do SDK .NET ou do `dotnet-ef` na máquina de quem executa.

## Correspondência de versão entre as duas imagens

Dada uma tag de versão `X.Y.Z`:

- `pedrozulian/mouts-sales-api:X.Y.Z` e `pedrozulian/mouts-sales-api-migrator:X.Y.Z` MUST ter
  sido produzidas a partir do mesmo commit.
- A estrutura de dados criada pelo migrator `X.Y.Z` MUST ser exatamente a que a aplicação
  `X.Y.Z` espera — verificado pelo smoke test descrito em
  [release-pipeline-contract.md](./release-pipeline-contract.md) antes da publicação, e
  observável depois via `/health` reportando saudável.
