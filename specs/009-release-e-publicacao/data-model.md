# Data Model: Release Automatizado e Publicação de Imagens

**Feature**: `009-release-e-publicacao` | **Date**: 2026-08-12

Esta feature não introduz nem altera nenhuma tabela, coluna ou entidade de domínio persistida —
não há migration de schema nesta entrega. As "entidades" abaixo são conceituais, do domínio de
release e empacotamento, e existem como artefatos de arquivo/pipeline, não como linhas de banco.
Documentadas aqui para rastrear a correspondência com a seção "Key Entities" da spec.

## Artefato executável

Unidade autocontida publicada no registro público, identificada por uma tag de imagem.

| Atributo | Descrição |
|---|---|
| Nome | `pedrozulian/mouts-sales-api` (aplicação) e `pedrozulian/mouts-sales-api-migrator` (preparação de schema) |
| Tags | Versão semântica exata (`1.2.0`) e `latest`, sempre as duas juntas na mesma publicação |
| Origem | `target: runtime` e `target: migrator` do `docker/Dockerfile` existente, sem stage novo |
| Plataforma | `linux/amd64`, única |
| Configuração aceita | Variáveis de ambiente (`ConnectionStrings__DefaultConnection`, `ASPNETCORE_ENVIRONMENT`); o artefato migrator adicionalmente aceita `--connection` como argumento de linha de comando |
| Invariante | As duas imagens de uma mesma publicação sempre compartilham a mesma versão — nunca é possível publicar uma sem a outra (FR-003, FR-006) |

## Versão

Identificador semântico determinado automaticamente a partir da natureza cumulativa dos commits
desde a versão anterior.

| Atributo | Descrição |
|---|---|
| Formato | `MAJOR.MINOR.PATCH`, seguindo SemVer |
| Origem | Tipo do commit Conventional Commits mais significativo desde a última tag (`feat` → MINOR, `fix`/`refactor`/`docs`/etc. sem `BREAKING CHANGE` → PATCH, `BREAKING CHANGE` → MAJOR) |
| Representação | Tag Git anotada + GitHub Release, ambas criadas no merge do PR de release |
| Relação | 1:1 com uma entrada do Histórico de mudanças; 1:2 com o par de Artefatos executáveis publicados sob essa tag |

## Histórico de mudanças

Documento acumulativo, gerado automaticamente, descrevendo o que mudou em cada versão.

| Atributo | Descrição |
|---|---|
| Localização | `CHANGELOG.md`, raiz do repositório |
| Estrutura | Uma seção por versão (mais recente primeiro), agrupada por tipo de mudança (Features, Bug Fixes, etc.) |
| Geração | Nunca editado manualmente — reescrito pelo release-please a cada PR de release, a partir do log de commits |
| Idioma | Segue o idioma dos commits que o originam (português, conforme o padrão já estabelecido no projeto) |

## Configuração de ambiente

Conjunto de valores fornecidos externamente ao artefato no momento da execução, nunca embutidos
na imagem.

| Variável | Obrigatória | Default na ausência | Consumidor |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | Sim, para ambos os artefatos | Nenhum — falha explícita na inicialização (FR-009, FR-010) | Aplicação e migrator |
| `ASPNETCORE_ENVIRONMENT` | Não | `Production` (FR-011) | Aplicação |
| `--connection` (argumento) | Não | Usa a variável de ambiente equivalente | Migrator, apenas |

Nenhuma dessas variáveis é um segredo gerenciado por esta feature além de estar fora da imagem —
a gestão por cofre dedicado está fora do escopo (ver Assumptions da spec).
