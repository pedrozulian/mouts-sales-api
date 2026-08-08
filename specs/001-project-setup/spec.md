# Feature Specification: Configuração Inicial do Projeto

**Feature Branch**: `001-project-setup`

**Created**: 2026-08-08

**Status**: Draft

**Input**: User description: "Configuração inicial do projeto e do ambiente de desenvolvimento da API de vendas (Sales API). Esta spec NÃO cobre funcionalidades de negócio (CRUD de vendas) — apenas a fundação técnica do projeto: estrutura de solução .NET 8 seguindo Clean Architecture (camadas Domain, Application, Infrastructure e API); configuração do Entity Framework Core com PostgreSQL; Swagger/OpenAPI habilitado para exploração dos endpoints; Mapster configurado para mapeamento de objetos; Mediator Pattern plugado na pipeline para disparo de eventos de domínio; logging estruturado configurado; projeto de testes com xUnit configurado e executável (mesmo com um teste trivial de smoke test); ambiente Docker com docker-compose contendo API, PostgreSQL e SonarQube Community Edition (para análise local de qualidade), subindo com um único comando; pipeline de CI/CD no GitHub Actions com steps de build, test e sonar, usando SonarCloud como gate de qualidade/cobertura (mínimo de 90%) sem depender de servidor próprio; README.md documentando propósito da aplicação, stack utilizada, pré-requisitos e passo a passo para preparar o ambiente e executar a aplicação localmente."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ambiente local sobe com um único comando (Priority: P1)

Como desenvolvedor que acabou de clonar o repositório, eu preciso conseguir subir todo o ambiente necessário (aplicação, banco de dados e ferramenta de análise de qualidade local) com um único comando, sem instalar nada manualmente além do Docker.

**Why this priority**: Sem um ambiente reproduzível, nenhuma outra atividade (desenvolver, testar, validar qualidade) pode acontecer. É o pré-requisito de tudo o mais nesta spec.

**Independent Test**: Em uma máquina limpa (só com Docker instalado), executar o comando de subida do ambiente e verificar que todos os recursos ficam disponíveis e saudáveis.

**Acceptance Scenarios**:

1. **Given** um clone limpo do repositório e Docker instalado, **When** o desenvolvedor executa o comando de subida do ambiente, **Then** a aplicação, o banco de dados e a ferramenta de análise de qualidade local ficam disponíveis sem passos manuais adicionais.
2. **Given** o ambiente já está no ar, **When** o desenvolvedor derruba e sobe o ambiente novamente, **Then** o estado volta a ficar saudável sem exigir limpeza manual.

---

### User Story 2 - Fundação de código em camadas pronta para receber implementação (Priority: P1)

Como desenvolvedor, eu preciso encontrar a solução já organizada em camadas (Domínio, Aplicação, Infraestrutura e API), com o mecanismo de comunicação desacoplada entre componentes, o mapeamento de objetos e o acesso a dados já plugados, para poder começar a implementar funcionalidades de negócio sem antes ter que montar a fundação técnica.

**Why this priority**: Define os limites arquiteturais que toda funcionalidade futura precisa respeitar. Se a fundação não existir ou não compilar, nenhuma funcionalidade de negócio pode ser iniciada.

**Independent Test**: Compilar a solução do zero e confirmar que as camadas existem, compilam, e que uma camada externa não é referenciada por uma camada mais interna.

**Acceptance Scenarios**:

1. **Given** a solução recém-clonada, **When** o desenvolvedor executa o build, **Then** todas as camadas compilam sem erro.
2. **Given** a estrutura de camadas definida, **When** se inspeciona as referências entre projetos, **Then** a camada de Domínio não depende de nenhuma camada mais externa.
3. **Given** a fundação pronta, **When** o desenvolvedor precisa persistir ou consultar dados, **Then** já existe um mecanismo de acesso a dados configurado e utilizável.

---

### User Story 3 - Fundação de testes automatizados pronta para TDD (Priority: P2)

Como desenvolvedor, eu preciso encontrar um projeto de testes automatizados já configurado e executável desde o primeiro commit, para poder seguir o fluxo de desenvolvimento orientado a testes desde a primeira funcionalidade.

**Why this priority**: O projeto adota desenvolvimento orientado a testes como prática obrigatória; se a fundação de testes não estiver pronta, essa prática não pode começar a ser seguida.

**Independent Test**: Rodar a suíte de testes logo após preparar o ambiente e confirmar que ela executa com sucesso, mesmo sem nenhuma funcionalidade de negócio implementada.

**Acceptance Scenarios**:

1. **Given** o ambiente preparado, **When** o desenvolvedor executa a suíte de testes, **Then** ela roda até o fim e reporta sucesso.
2. **Given** a suíte de testes configurada, **When** um novo teste é adicionado a um dos projetos de teste, **Then** ele é descoberto e executado automaticamente pela mesma suíte.

---

### User Story 4 - Exploração e verificação de saúde da API (Priority: P2)

Como desenvolvedor (ou avaliador do projeto), eu preciso conseguir abrir a documentação interativa da API e confirmar que a aplicação está no ar e suas dependências críticas estão saudáveis, mesmo antes de qualquer endpoint de negócio existir.

**Why this priority**: Permite validar visualmente que a fundação técnica está funcional de ponta a ponta (aplicação + banco), sem depender de código de negócio.

**Independent Test**: Com o ambiente no ar, acessar a documentação interativa e o endpoint de verificação de saúde diretamente pelo navegador ou por uma ferramenta HTTP.

**Acceptance Scenarios**:

1. **Given** o ambiente no ar, **When** o desenvolvedor acessa a documentação interativa da API, **Then** a interface carrega e lista os endpoints disponíveis.
2. **Given** o ambiente no ar, **When** o desenvolvedor consulta o endpoint de verificação de saúde, **Then** recebe uma resposta indicando que a aplicação e o banco de dados estão saudáveis.

---

### User Story 5 - Pipeline de integração contínua com gate de qualidade automatizado (Priority: P3)

Como responsável técnico pelo projeto, eu preciso que toda alteração proposta ao repositório seja automaticamente compilada, testada e analisada quanto à qualidade e cobertura de código, para garantir que o padrão mínimo definido para o projeto seja respeitado sem depender de revisão manual.

**Why this priority**: Automatiza a verificação da política de qualidade (cobertura mínima) definida para o projeto, mas só passa a ter valor depois que existe algo para compilar e testar (Histórias 1 a 3).

**Independent Test**: Abrir uma alteração no repositório e observar o pipeline executando build, testes e análise de qualidade, bloqueando a integração se a cobertura mínima não for atingida.

**Acceptance Scenarios**:

1. **Given** uma alteração proposta ao repositório, **When** o pipeline é executado, **Then** ele reporta o resultado de build, testes e análise de qualidade/cobertura.
2. **Given** uma alteração que reduz a cobertura de testes abaixo do mínimo definido, **When** o pipeline é executado, **Then** a integração da alteração é bloqueada.
3. **Given** uma alteração que atende a todos os critérios de qualidade, **When** o pipeline é executado, **Then** a alteração fica liberada para integração.

---

### User Story 6 - Onboarding autoguiado via README (Priority: P3)

Como novo desenvolvedor (ou avaliador do projeto) sem contato prévio com o time, eu preciso conseguir entender o propósito da aplicação e colocá-la para rodar localmente seguindo apenas a documentação do repositório, sem precisar de explicações adicionais de outra pessoa.

**Why this priority**: Reduz a dependência de conhecimento tácito, mas só é plenamente verificável depois que o ambiente (História 1) e a aplicação (História 2) já existem para serem documentados com precisão.

**Independent Test**: Uma pessoa sem contexto prévio segue apenas o README, do zero, e chega a uma aplicação rodando localmente.

**Acceptance Scenarios**:

1. **Given** o README do repositório, **When** um novo desenvolvedor o segue do início ao fim, **Then** ele identifica os pré-requisitos, prepara o ambiente e executa a aplicação sem precisar de ajuda externa.
2. **Given** o README do repositório, **When** um novo desenvolvedor o lê, **Then** ele entende o propósito da aplicação e a stack utilizada antes mesmo de rodar qualquer comando.

---

### Edge Cases

- O que acontece quando o desenvolvedor tenta subir o ambiente sem o Docker instalado ou em execução? O sistema deve falhar com uma mensagem que indique a causa, e o README deve alertar sobre esse pré-requisito.
- O que acontece quando uma porta usada por um dos recursos (aplicação, banco de dados, análise de qualidade local) já está em uso na máquina do desenvolvedor?
- O que acontece quando ainda não existe nenhum código de negócio implementado — a cobertura mínima de testes deve ser tratada como satisfeita, já que não há código sem cobertura?
- O que acontece quando o serviço externo de análise de qualidade usado pelo pipeline está indisponível no momento da execução? O pipeline deve falhar de forma explícita, nunca liberar a integração silenciosamente.
- O que acontece quando o banco de dados não está disponível no momento em que a aplicação sobe? A verificação de saúde deve refletir essa indisponibilidade.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST ser estruturado em camadas (Domínio, Aplicação, Infraestrutura e API), com dependências apontando somente das camadas externas para as internas.
- **FR-002**: O sistema MUST disponibilizar um projeto de testes automatizados executável desde o primeiro commit, capaz de validar a fundação técnica mesmo sem código de negócio.
- **FR-003**: O sistema MUST expor documentação interativa listando os endpoints disponíveis.
- **FR-004**: O sistema MUST expor um mecanismo de verificação de saúde que reporte o status da aplicação e de suas dependências críticas (banco de dados).
- **FR-005**: O sistema MUST ter um mecanismo de acesso a um banco de dados relacional já configurado e utilizável, mesmo sem entidades de negócio ainda definidas.
- **FR-006**: O sistema MUST ter um mecanismo de comunicação desacoplada entre componentes já configurado, pronto para receber handlers de comando, consulta e evento de domínio no futuro.
- **FR-007**: O sistema MUST ter um mecanismo de mapeamento entre objetos já configurado e utilizável.
- **FR-008**: O sistema MUST registrar logs estruturados das operações relevantes, com informações suficientes para rastrear uma requisição de ponta a ponta.
- **FR-009**: Todos os recursos necessários para executar a aplicação (aplicação, banco de dados, ferramenta de análise de qualidade local) MUST poder ser iniciados com um único comando, sem instalação manual de dependências além da ferramenta de containerização.
- **FR-010**: O sistema MUST possuir um pipeline de integração contínua que executa, a cada alteração proposta ao repositório, a compilação da aplicação, a execução da suíte de testes automatizados e uma análise de qualidade/cobertura de código.
- **FR-011**: O pipeline de integração contínua MUST bloquear a integração de uma alteração quando a cobertura de testes ficar abaixo do mínimo definido para o projeto (90%) ou quando a análise de qualidade reportar falhas críticas.
- **FR-012**: A análise de qualidade de código MUST poder ser executada também localmente pelo desenvolvedor, antes de propor uma alteração, sem depender de um serviço externo.
- **FR-013**: O repositório MUST conter documentação descrevendo o propósito da aplicação, a stack utilizada, os pré-requisitos de ambiente e o passo a passo para preparar e executar a aplicação localmente.
- **FR-014**: A documentação, os comentários de código e as mensagens do histórico de alterações MUST estar em português.
- **FR-015**: Um novo desenvolvedor MUST conseguir preparar o ambiente completo e validar que ele está funcional sem depender de conhecimento não documentado.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um novo desenvolvedor consegue preparar o ambiente completo e ter a aplicação rodando localmente em até 15 minutos, seguindo apenas a documentação do repositório.
- **SC-002**: 100% dos recursos necessários para executar a aplicação sobem através de um único comando, sem nenhum passo manual adicional.
- **SC-003**: A suíte de testes automatizados executa com sucesso (sem falhas) imediatamente após a preparação do ambiente, mesmo sem código de negócio implementado.
- **SC-004**: A cada alteração proposta ao repositório, é possível verificar automaticamente — sem intervenção manual — se a cobertura de testes atende ao mínimo definido para o projeto.
- **SC-005**: A aplicação permite confirmar, sem inspecionar código-fonte, que ela e sua dependência de banco de dados estão em funcionamento.
- **SC-006**: O pipeline de integração contínua conclui build, testes e análise de qualidade em até 10 minutos por execução.

## Assumptions

- O repositório já está hospedado no GitHub; o pipeline de integração contínua roda nessa plataforma.
- Uma conta gratuita no serviço de análise de qualidade em nuvem (usado pelo pipeline) será criada e configurada por quem administra o projeto antes da execução do pipeline; esta spec cobre a integração, não a criação da conta em si.
- O ambiente de trabalho de cada desenvolvedor já possui a ferramenta de containerização instalada; instalar essa ferramenta está fora do escopo desta spec.
- Ainda não existem entidades de negócio (ex.: Venda, Produto, Cliente) nesta etapa — esta spec cobre exclusivamente a fundação técnica do projeto, sem funcionalidades de CRUD.
- A análise de qualidade local (rodando via containerização) é usada manualmente pelo desenvolvedor antes de propor uma alteração; quem bloqueia formalmente a integração é o pipeline de integração contínua.
