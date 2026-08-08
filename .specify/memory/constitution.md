<!--
Sync Impact Report
Version change: 1.0.0 → 1.0.1 (patch — esclarecimento não semântico)
Modified principles:
  - IX. Qualidade de Código — esclarecido que o gate de CI usa SonarCloud e a análise local
    usa SonarQube Community Edition via Docker (mesma regra de negócio, apenas explicitação
    da ferramenta em cada ambiente)
Added sections: nenhuma
Removed sections: nenhuma
Templates requiring updates:
  - .specify/templates/plan-template.md ⚠ pending manual review (Constitution Check gate deve referenciar estes 10 princípios)
  - .specify/templates/spec-template.md ✅ sem referência direta a princípios, nenhuma mudança necessária
  - .specify/templates/tasks-template.md ⚠ pending manual review (gates de TDD/cobertura devem se refletir nas categorias de tasks)
  - .specify/templates/checklist-template.md ✅ nenhuma mudança necessária
Follow-up TODOs: nenhum
-->
# Mouts Sales API Constitution

## Core Principles

### I. Domain-Driven Design (DDD)

O domínio é modelado de forma explícita: entidades, agregados, objetos de valor, eventos de
domínio e bounded contexts MUST refletir a linguagem ubíqua do negócio de vendas. Regras de
negócio MUST residir na camada de Domain, nunca em controllers, serviços de infraestrutura ou
camadas de apresentação. Toda nova funcionalidade começa pela modelagem do domínio antes de
qualquer decisão técnica de persistência ou transporte.

Rationale: evita anemia de domínio e mantém a lógica de negócio testável e isolada de detalhes
de implementação.

### II. Test-Driven Development (TDD) (NÃO NEGOCIÁVEL)

Nenhuma linha de código de produção é escrita sem um teste que falhe antes. O ciclo
Red-Green-Refactor MUST ser seguido em toda implementação: escrever o teste, vê-lo falhar,
implementar o mínimo necessário para passar, refatorar. Pull requests que não sigam esse fluxo
(evidenciado pelo histórico de commits ou pela ausência de testes) MUST ser rejeitados.

Rationale: garante cobertura desde a origem, reduz regressões e força um design testável.

### III. Princípios SOLID

Toda classe, método e módulo MUST respeitar SRP, OCP, LSP, ISP e DIP. Dependências MUST ser
injetadas via construtor e abstraídas por interfaces quando cruzam limites de camada. Revisões
de código MUST sinalizar violações (ex.: classes com múltiplas responsabilidades, dependências
concretas de infraestrutura dentro do domínio).

Rationale: mantém o código extensível e testável à medida que o domínio de vendas cresce.

### IV. Documentação e Comunicação em Português

README, comentários de código, descrições de commits, Pull Requests, issues e demais artefatos
de documentação MUST ser escritos em português. Identificadores de código (nomes de classes,
métodos, variáveis, namespaces) MUST permanecer em inglês, seguindo a convenção usual do
ecossistema .NET.

Rationale: alinha a comunicação do time ao idioma do projeto sem abrir mão da compatibilidade
com bibliotecas, frameworks e convenções da stack .NET, que são majoritariamente em inglês.

### V. Arquitetura em Camadas (Clean Architecture)

A solução MUST ser organizada em camadas Domain, Application, Infrastructure e API
(apresentação). Dependências MUST apontar sempre para dentro: Domain não conhece Application,
Infrastructure ou API; Application não conhece Infrastructure ou API. Comunicação entre camadas
externas e o Domain MUST ocorrer por meio de interfaces definidas no Domain ou Application e
implementadas em Infrastructure.

Rationale: isola regras de negócio de detalhes de banco de dados, framework web e bibliotecas
externas, permitindo substituí-los sem alterar o domínio.

### VI. Eventos de Domínio via Mediator Pattern

Comunicação entre agregados, handlers de comando/query e efeitos colaterais entre domínios
MUST ocorrer via eventos de domínio disparados através do Mediator Pattern. Handlers MUST ser
desacoplados entre si — um agregado nunca invoca diretamente serviços de outro domínio.

Rationale: mantém bounded contexts desacoplados e possibilita evoluir cada domínio de forma
independente.

### VII. Result/Notification Pattern

Erros de validação e de regras de negócio MUST ser comunicados como retorno explícito
(`Result`/`Notification`), não como exceptions lançadas. Exceptions MUST ser reservadas para
situações verdadeiramente excepcionais (falhas de infraestrutura, invariantes de programação
violadas).

Rationale: fluxo de erro previsível e testável, sem custo de performance nem controle de fluxo
via exceptions.

### VIII. Observabilidade

Logging estruturado MUST ser aplicado em toda operação relevante (comandos, queries, eventos de
domínio, integrações externas), com informações suficientes para rastrear uma requisição de
ponta a ponta. A API MUST expor endpoint(s) de health check cobrindo suas dependências críticas
(banco de dados, etc.).

Rationale: rastreabilidade e diagnóstico rápido de problemas em produção são requisitos desde o
primeiro deploy.

### IX. Qualidade de Código

Toda funcionalidade MUST manter cobertura mínima de testes de 90%, verificada automaticamente
no pipeline de CI via SonarCloud (gate de qualidade que bloqueia o merge em caso de falha).
Localmente, o desenvolvedor MUST poder rodar a mesma análise de qualidade contra a instância
de SonarQube Community Edition disponibilizada no ambiente Docker, antes de abrir o PR. Análise
estática MUST rodar no build e falhas de qualidade (code smells críticos, vulnerabilidades,
duplicação excessiva) MUST bloquear o merge.

Rationale: garante que o padrão de qualidade seja mensurável e não dependa apenas de revisão
manual.

### X. Ambiente 100% Reprodutível via Docker

Todo recurso necessário para rodar a aplicação (API, banco de dados PostgreSQL, SonarQube)
MUST possuir definição Docker e ser orquestrado via docker-compose. Um novo desenvolvedor MUST
conseguir preparar o ambiente completo com um único comando, sem instalação manual de
dependências além do Docker.

Rationale: elimina divergência de ambiente ("funciona na minha máquina") e simplifica onboarding
e CI.

## Stack Tecnológica Obrigatória

- **Runtime/Framework**: .NET 8.0.
- **Testes unitários**: xUnit.
- **Documentação de API**: Swagger/OpenAPI.
- **Mapeamento de objetos**: Mapster.
- **Banco de dados**: PostgreSQL.
- **ORM**: Entity Framework Core.
- **Containerização**: Docker e Docker Compose para todos os recursos (API, banco, SonarQube).
- **Qualidade de código (CI)**: SonarCloud (SaaS) como gate de qualidade/cobertura no pipeline
  de CI/CD, sem necessidade de hospedar servidor próprio.
- **Qualidade de código (local)**: SonarQube Community Edition via Docker, parte do
  docker-compose do ambiente de desenvolvimento, para análise antes do push/PR.
- **CI/CD**: GitHub Actions, com no mínimo os steps de build, test e sonar (gate de cobertura
  via SonarCloud).

Mudanças de stack (troca de biblioteca, banco de dados ou provedor de CI/CD) MUST passar por
amendment desta constitution antes de serem adotadas.

## Fluxo de Desenvolvimento e Gates de Qualidade

- Todo desenvolvimento MUST iniciar pelo teste (Princípio II), independentemente do tamanho da
  mudança.
- O pipeline de CI/CD MUST executar, nesta ordem, build → test → sonar; falha em qualquer step
  MUST bloquear o merge.
- Pull requests MUST ser revisados quanto à aderência à arquitetura em camadas (Princípio V) e
  aos princípios SOLID (Princípio III) antes da aprovação.
- Nenhuma regra de negócio MUST ser implementada fora da camada de Domain, mesmo por
  conveniência ou prazo.

## Governance

Esta constitution prevalece sobre qualquer outra prática, convenção de equipe ou preferência
individual em caso de conflito. Toda proposta de spec, plano ou implementação MUST ser
verificada quanto à conformidade com os princípios aqui definidos antes de ser aceita.

Emendas a esta constitution MUST ser documentadas explicitamente, incluindo a razão da mudança,
o princípio afetado e a data. A versão MUST seguir versionamento semântico:

- **MAJOR**: remoção ou redefinição incompatível de princípios existentes.
- **MINOR**: adição de novo princípio ou seção, ou expansão material de uma diretriz existente.
- **PATCH**: esclarecimentos, correções de texto ou refinamentos não semânticos.

Toda revisão de código e todo plano técnico (`/speckit-plan`) MUST verificar conformidade com
esta constitution. Complexidade adicional (ex.: nova camada, novo padrão) MUST ser justificada
explicitamente na spec ou no plano correspondente.

**Version**: 1.0.1 | **Ratified**: 2026-08-08 | **Last Amended**: 2026-08-08
