# Feature Specification: Confiabilidade Operacional e Consistência de Dados

**Feature Branch**: `008-confiabilidade-e-consistencia`

**Created**: 2026-08-11

**Status**: Draft

**Input**: User description: "Iniciativa de engenharia que torna a Sales API provisionável de forma confiável e exata no que persiste. Dois eixos: operação (provisionamento do schema, contrato de erro, diagnóstico) e dados (precisão monetária, integridade de invariante, padronização do modelo físico). A API já implementa os seis casos de uso (UC-01 a UC-06); esta feature não adiciona caso de uso novo — corrige defeitos de correção confirmados por execução real e alinha o entregue à documentação de domínio existente. Escopo: (1) provisionamento do schema como etapa própria do ambiente, hoje inexistente; (2) verificação de saúde que detecte schema desatualizado; (3) padronização dos nomes do modelo físico; (4) arredondamento monetário em duas casas; (5) tradução uniforme de falhas inesperadas para o contrato de erro da API; (6) unicidade de produto por venda considerando itens cancelados; (7) remoção de resíduos de scaffolding e eliminação de regra duplicada; (8) reescrita do README. Fora de escopo: novos casos de uso, autenticação, outbox pattern, value object Money, keyset pagination, publicação em broker real."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ambiente provisionado e pronto para uso (Priority: P1)

Uma pessoa que nunca rodou o projeto prepara o ambiente completo a partir de um estado limpo, seguindo a documentação, e consegue registrar uma venda imediatamente — sem executar nenhuma etapa manual de preparação do banco de dados, sem instalar ferramentas além das já exigidas e sem precisar descobrir por conta própria que faltou algum passo.

**Why this priority**: É a condição de existência de todas as demais. Hoje o ambiente sobe, reporta-se saudável e **todas as operações de escrita falham**, porque a estrutura do banco nunca é criada. Nenhuma outra qualidade do sistema é observável enquanto isso não for resolvido, e o princípio de ambiente reprodutível com um único comando está formalmente descumprido.

**Independent Test**: Pode ser testada isoladamente partindo de um ambiente sem nenhum dado ou estrutura preexistente, executando apenas o comando de preparação documentado e, em seguida, registrando uma venda e consultando-a — sem nenhuma intervenção adicional entre os dois passos.

**Acceptance Scenarios**:

1. **Given** um ambiente limpo, sem nenhuma estrutura de banco de dados criada anteriormente, **When** o comando único de preparação documentado é executado, **Then** o ambiente fica operacional e o registro de uma venda é concluído com sucesso na primeira tentativa.
2. **Given** um ambiente já preparado anteriormente, **When** o comando de preparação é executado novamente, **Then** a preparação da estrutura de dados é reaplicada de forma idempotente, sem erro e sem perda dos dados existentes.
3. **Given** um ambiente em que a preparação da estrutura de dados falha, **When** o ambiente é iniciado, **Then** a aplicação não é disponibilizada como pronta para receber requisições, e a causa da falha fica registrada de forma legível.
4. **Given** o ambiente operacional, **When** a estrutura de dados é preparada, **Then** essa preparação ocorre como etapa própria e concluída antes de a aplicação começar a atender requisições, e não como efeito colateral da inicialização da aplicação.

---

### User Story 2 - Valores monetários exatos e estáveis (Priority: P1)

Um sistema cliente registra uma venda cujos cálculos de desconto produzem frações menores que um centavo. O valor que ele recebe na resposta do registro é exatamente o mesmo que obterá em qualquer consulta futura da mesma venda, e o total geral corresponde exatamente à soma dos totais dos itens ativos.

**Why this priority**: Trata-se de dado financeiro incorreto sendo persistido. Hoje o mesmo registro tem representações diferentes conforme a operação que o devolve, e o total geral gravado pode divergir da soma dos itens gravados — violando a invariante de que o total da venda é a soma dos itens não cancelados. É o tipo de defeito que não se manifesta em valores "redondos" e passa despercebido até virar divergência contábil.

**Independent Test**: Pode ser testada isoladamente registrando uma venda com quantidade e preço unitário que produzam desconto com mais de duas casas decimais, comparando a resposta do registro com a resposta da consulta subsequente, e conferindo que o total geral é igual à soma dos totais dos itens.

**Acceptance Scenarios**:

1. **Given** uma venda cujos itens produzem desconto com mais de duas casas decimais, **When** a venda é registrada, **Then** o desconto, o total do item e o total da venda são expressos com no máximo duas casas decimais.
2. **Given** uma venda registrada nessas condições, **When** ela é consultada em seguida, **Then** todos os valores monetários retornados são idênticos aos devolvidos no momento do registro.
3. **Given** uma venda com múltiplos itens cujos totais individuais foram arredondados, **When** o total geral da venda é apurado, **Then** ele é exatamente igual à soma dos totais dos itens ativos, sem diferença de centavos.
4. **Given** uma venda alterada de modo que a quantidade de um item mude de faixa de desconto, **When** a alteração é concluída, **Then** os valores recalculados seguem a mesma regra de arredondamento aplicada no registro.
5. **Given** um valor de desconto cuja terceira casa decimal é exatamente cinco, **When** o arredondamento é aplicado, **Then** o valor é arredondado para cima em valor absoluto, de forma consistente em toda a aplicação.

---

### User Story 3 - Toda falha responde no mesmo contrato (Priority: P2)

Um sistema cliente que integra com a API consegue tratar qualquer resposta de erro com um único caminho de código, porque todas as falhas — inclusive as inesperadas — chegam no mesmo formato, sem expor detalhes internos do sistema.

**Why this priority**: A API já se compromete com um contrato de erro único e chega a abrir mão de conveniências do framework para honrá-lo. Hoje esse compromisso quebra exatamente onde mais importa para quem integra: falhas não previstas retornam em formato diferente e podem expor detalhes internos de implementação no corpo da resposta. Depende da existência do contrato, já estabelecido, mas não de nenhuma outra história desta feature.

**Independent Test**: Pode ser testada isoladamente provocando uma falha inesperada durante o processamento de uma requisição e verificando que a resposta segue o mesmo formato de erro das rejeições de regra de negócio, sem conteúdo interno de diagnóstico.

**Acceptance Scenarios**:

1. **Given** uma requisição cujo processamento falha por uma condição inesperada, **When** a resposta é devolvida, **Then** ela segue o mesmo formato de erro usado nas rejeições de regra de negócio, com chave e mensagem.
2. **Given** uma falha inesperada em qualquer ambiente de execução, **When** a resposta é devolvida, **Then** ela não contém rastro de pilha, nome de tipo interno, texto de exceção original nem qualquer detalhe de implementação.
3. **Given** uma falha inesperada, **When** ela ocorre, **Then** a causa original é registrada de forma estruturada e correlacionável à requisição que a originou, ainda que não seja exposta ao solicitante.
4. **Given** uma requisição rejeitada por violação de regra de negócio, **When** a resposta é devolvida, **Then** seu formato e seu significado permanecem exatamente como estão hoje, sem alteração de comportamento.

---

### User Story 4 - Um produto ocupa uma única linha ao longo de toda a vida da venda (Priority: P2)

Um sistema cliente que tenta reintroduzir, em uma alteração de venda, um produto que já figura na venda — mesmo que a linha correspondente esteja cancelada — recebe uma recusa clara e acionável, e não uma falha genérica de sistema.

**Why this priority**: A regra de que um produto aparece no máximo uma vez por venda existe para impedir que o limite de unidades por produto seja contornado por linhas duplicadas. Hoje essa regra é aplicada de duas formas divergentes em pontos diferentes do sistema, e a divergência se manifesta como falha inesperada em vez de recusa de negócio. Afeta um caminho legítimo de uso e degrada a experiência de integração, mas é menos frequente do que as histórias P1.

**Independent Test**: Pode ser testada isoladamente registrando uma venda com dois itens, cancelando um deles, e em seguida solicitando uma alteração que reintroduza o produto do item cancelado como item novo — verificando que a resposta é uma recusa de regra de negócio identificando o produto em conflito.

**Acceptance Scenarios**:

1. **Given** uma venda com um item cancelado referente a um produto, **When** uma alteração tenta adicionar esse mesmo produto como item novo, **Then** o sistema recusa a alteração informando o produto duplicado, sem alterar qualquer estado da venda.
2. **Given** uma venda com um item cancelado referente a um produto, **When** uma alteração é solicitada sem reintroduzir esse produto, **Then** a alteração é concluída normalmente.
3. **Given** uma tentativa de reintroduzir o produto de um item cancelado, **When** a recusa ocorre, **Then** ela é expressa como violação de regra de negócio, identificando qual item do corpo da requisição causou o conflito — nunca como falha inesperada de sistema.
4. **Given** qualquer tentativa de alteração recusada por esse motivo, **When** a venda é consultada em seguida, **Then** seu estado e o de todos os seus itens permanecem exatamente como estavam antes da tentativa.

---

### User Story 5 - Documentação de entrada reflete o sistema atual (Priority: P2)

Uma pessoa que chega ao repositório pela primeira vez entende, apenas pela documentação de entrada, o que o sistema faz, quais operações ele oferece, quais regras de negócio ele aplica e por que as principais decisões de desenho foram tomadas — e consegue colocá-lo para rodar seguindo o passo a passo descrito.

**Why this priority**: A documentação de entrada descreve o projeto em um estágio que ele deixou para trás há várias entregas: não menciona nenhuma das operações disponíveis nem nenhuma regra de negócio. Para quem chega ao repositório, isso é a primeira e às vezes única impressão do sistema. Depende da User Story 1 estar concluída para que o passo a passo descrito seja verdadeiro.

**Independent Test**: Pode ser testada isoladamente entregando o repositório a alguém sem contexto prévio e sem acesso a nenhuma outra fonte de documentação, e verificando que essa pessoa consegue colocar o sistema no ar e registrar uma venda usando apenas a documentação de entrada.

**Acceptance Scenarios**:

1. **Given** uma pessoa sem contexto prévio sobre o projeto, **When** ela lê apenas a documentação de entrada, **Then** ela identifica o propósito do sistema, todas as operações disponíveis e as regras de desconto aplicadas.
2. **Given** essa mesma pessoa, **When** ela segue o passo a passo de preparação descrito, **Then** o ambiente sobe e ela consegue registrar uma venda sem consultar nenhuma outra fonte.
3. **Given** a documentação de entrada, **When** ela é lida por completo, **Then** ela explica as decisões de desenho que um leitor questionaria — por que a exclusão é lógica, por que cliente, filial e produto não têm cadastro próprio, e como a estrutura de dados é preparada em cada ambiente.
4. **Given** a documentação de entrada, **When** ela é comparada ao comportamento real do sistema, **Then** não há afirmação desatualizada, etapa ausente nem descrição de estágio de desenvolvimento já superado.

---

### User Story 6 - Modelo físico padronizado e diretamente consultável (Priority: P3)

Uma pessoa que precisa inspecionar os dados diretamente no banco — para diagnosticar um problema, conferir um cálculo ou explorar o modelo — escreve consultas de forma natural, sem precisar descobrir caso a caso qual identificador exige tratamento especial.

**Why this priority**: Hoje o modelo físico mistura duas convenções de nomenclatura na mesma tabela, o que obriga a delimitar parte dos identificadores em consultas manuais e diverge do modelo de dados documentado. É atrito real de diagnóstico e uma inconsistência visível para quem revisa o sistema, mas nenhuma funcionalidade depende disso para operar corretamente.

**Independent Test**: Pode ser testada isoladamente inspecionando a estrutura das tabelas e executando consultas manuais sobre todas as colunas sem nenhum tratamento especial de identificador, além de comparar a estrutura resultante com o modelo de dados documentado.

**Acceptance Scenarios**:

1. **Given** o modelo de dados provisionado, **When** sua estrutura é inspecionada, **Then** todas as tabelas, colunas, índices e restrições seguem uma convenção única de nomenclatura, sem exceções.
2. **Given** o modelo de dados provisionado, **When** consultas manuais são escritas sobre qualquer coluna de qualquer tabela, **Then** nenhum identificador precisa de delimitação especial para ser reconhecido.
3. **Given** o modelo de dados provisionado, **When** ele é comparado ao modelo de dados documentado, **Then** os nomes de tabelas, colunas e restrições correspondem ao que está documentado.
4. **Given** um ambiente com dados já existentes, **When** a padronização é aplicada, **Then** os dados são preservados integralmente e todas as operações da API continuam funcionando sem alteração de comportamento observável.

---

### User Story 7 - Base de código sem resíduos e sem regra duplicada (Priority: P3)

Uma pessoa que revisa o código encontra apenas elementos que pertencem ao domínio de vendas, e cada regra de negócio expressa em um único lugar — sem exemplos remanescentes de configuração inicial e sem validações replicadas que possam divergir entre si.

**Why this priority**: São resíduos e duplicações que não causam defeito hoje, mas criam risco de divergência futura (a mesma regra escrita duas vezes) e ruído para quem revisa (tipos de exemplo sem relação com o domínio). Melhora a sustentação do código sem alterar comportamento, por isso é a menor prioridade.

**Independent Test**: Pode ser testada isoladamente verificando que a suíte permanece integralmente verde e a cobertura se mantém acima do mínimo exigido após a remoção e a unificação, sem nenhuma alteração de comportamento observável na API.

**Acceptance Scenarios**:

1. **Given** a base de código, **When** ela é inspecionada, **Then** não existem tipos de exemplo ou demonstração remanescentes da configuração inicial do projeto, sem relação com o domínio de vendas.
2. **Given** as verificações automatizadas que validavam o registro dos mecanismos de mediação e de mapeamento, **When** os tipos de exemplo são removidos, **Then** essas verificações continuam existindo, exercendo agora tipos reais do domínio de vendas.
3. **Given** as regras de validação de quantidade e preço unitário de um item, **When** a base de código é inspecionada, **Then** elas estão expressas em um único lugar, usado por todos os caminhos que criam ou alteram um item.
4. **Given** a tradução de resultado de operação para resposta da API, **When** a base de código é inspecionada, **Then** ela está expressa em um único lugar, usado por todas as operações, sem variações não intencionais entre elas.
5. **Given** as operações da API, **When** qualquer uma delas é executada, **Then** ela registra informação estruturada suficiente para rastreamento, sem exceção.
6. **Given** o ambiente sendo iniciado, **When** a aplicação sobe, **Then** nenhum aviso de configuração inconsistente é registrado.

---

### Edge Cases

- O que acontece quando a preparação da estrutura de dados é executada em um ambiente onde ela já foi aplicada anteriormente?
- Como o sistema se comporta quando a aplicação é iniciada apontando para uma base cuja estrutura está desatualizada em relação à versão do código?
- O que a verificação de saúde deve reportar quando a base está acessível mas sua estrutura não corresponde à esperada pela aplicação?
- Como o sistema se comporta quando duas instâncias da aplicação são iniciadas simultaneamente contra a mesma base ainda não preparada?
- O que acontece com o total geral de uma venda quando vários itens têm seus totais arredondados individualmente e a soma dos arredondamentos difere do arredondamento da soma?
- Como o arredondamento se comporta quando o valor a arredondar está exatamente no ponto médio entre dois centavos?
- Como o sistema se comporta quando uma alteração tenta reintroduzir um produto de item cancelado ao mesmo tempo em que referencia corretamente os demais itens ativos?
- O que acontece quando uma alteração reintroduz um produto de item cancelado e, no mesmo corpo, viola outra regra — todos os erros são reportados juntos?
- Como as consultas e operações existentes se comportam durante e após a padronização dos nomes do modelo físico, em um ambiente que já contém dados?
- O que a API responde quando ocorre uma falha inesperada em uma requisição que já havia passado por todas as validações de negócio?

## Requirements *(mandatory)*

### Functional Requirements

**Provisionamento e diagnóstico**

- **FR-001**: O sistema MUST preparar a estrutura de dados necessária à sua operação como etapa própria do provisionamento do ambiente, concluída antes de a aplicação passar a atender requisições.
- **FR-002**: O sistema MUST permitir que o ambiente completo seja preparado a partir de um estado limpo com um único comando, sem etapas manuais adicionais e sem exigir ferramentas além das já documentadas como pré-requisito.
- **FR-003**: O sistema MUST NOT preparar a estrutura de dados como efeito colateral da inicialização da aplicação.
- **FR-004**: O sistema MUST tornar a preparação da estrutura de dados idempotente — executá-la novamente sobre um ambiente já preparado MUST NOT causar erro nem perda de dados.
- **FR-005**: O sistema MUST impedir que a aplicação seja considerada pronta para receber requisições quando a preparação da estrutura de dados não tiver sido concluída com sucesso.
- **FR-006**: A verificação de saúde MUST reportar estado não saudável quando a estrutura de dados estiver ausente ou desatualizada em relação à esperada pela aplicação, ainda que a base esteja acessível.
- **FR-007**: A verificação de saúde MUST preservar seu contrato de resposta atual, reportando o estado geral e o estado individual de cada dependência verificada.

**Exatidão de valores monetários**

- **FR-008**: O sistema MUST expressar todo valor monetário derivado — desconto do item, total do item e total da venda — com no máximo duas casas decimais.
- **FR-009**: O sistema MUST aplicar o arredondamento no momento do cálculo, de forma que o valor devolvido em uma operação de escrita seja idêntico ao valor devolvido em qualquer consulta posterior do mesmo registro.
- **FR-010**: O sistema MUST arredondar valores cujo ponto de corte esteja exatamente no meio afastando-se de zero, de maneira uniforme em toda a aplicação.
- **FR-011**: O sistema MUST garantir que o total geral de uma venda seja exatamente igual à soma dos totais dos seus itens não cancelados, após qualquer operação que altere itens ou valores.
- **FR-012**: O sistema MUST aplicar a mesma regra de arredondamento em todos os caminhos que produzem valores derivados, incluindo o registro e a alteração de venda.

**Contrato de erro**

- **FR-013**: O sistema MUST responder a qualquer falha inesperada no mesmo formato de erro usado nas rejeições de regra de negócio.
- **FR-014**: O sistema MUST NOT expor rastro de pilha, texto de exceção original, nome de tipo interno ou qualquer outro detalhe de implementação no corpo de uma resposta de erro, em nenhum ambiente de execução.
- **FR-015**: O sistema MUST registrar, de forma estruturada e correlacionável à requisição de origem, a causa original de toda falha inesperada.
- **FR-016**: O sistema MUST preservar sem alteração o formato e o significado das respostas de erro já existentes para rejeições de regra de negócio e recursos não encontrados.

**Integridade da unicidade de produto**

- **FR-017**: O sistema MUST considerar os itens cancelados ao verificar se um produto já figura em uma venda, recusando a introdução de um item cujo produto já esteja presente na venda em qualquer estado.
- **FR-018**: O sistema MUST expressar essa recusa como violação de regra de negócio, identificando o item do corpo da requisição que causou o conflito, e MUST NOT permitir que ela se manifeste como falha inesperada.
- **FR-019**: O sistema MUST NOT alterar qualquer estado da venda quando uma alteração for recusada por esse motivo.

**Modelo físico**

- **FR-020**: O modelo físico de dados MUST seguir uma convenção única de nomenclatura em todas as tabelas, colunas, índices, chaves e demais restrições.
- **FR-021**: A convenção adotada MUST permitir que qualquer identificador seja referenciado em consultas manuais sem necessidade de delimitação especial.
- **FR-022**: Os nomes do modelo físico MUST corresponder aos nomes descritos no modelo de dados documentado do projeto.
- **FR-023**: A padronização MUST preservar integralmente os dados existentes e MUST NOT alterar nenhum comportamento observável da API.

**Sustentação da base de código**

- **FR-024**: A base de código MUST NOT conter tipos de exemplo ou demonstração remanescentes da configuração inicial do projeto, sem relação com o domínio de vendas.
- **FR-025**: As verificações automatizadas que validam o registro dos mecanismos de mediação e de mapeamento MUST ser preservadas, passando a exercer tipos reais do domínio de vendas.
- **FR-026**: As regras de validação de quantidade e preço unitário de um item MUST estar expressas em um único lugar, compartilhado por todos os caminhos que criam ou alteram um item.
- **FR-027**: A tradução do resultado de uma operação para a resposta da API MUST estar expressa em um único lugar, compartilhado por todas as operações.
- **FR-028**: Toda operação da API MUST registrar informação estruturada suficiente para rastreamento, sem exceção.
- **FR-029**: A inicialização da aplicação MUST NOT registrar aviso de configuração inconsistente com o ambiente em que ela roda.

**Documentação**

- **FR-030**: A documentação de entrada do repositório MUST descrever o propósito do sistema, todas as operações disponíveis e as regras de negócio aplicadas.
- **FR-031**: A documentação de entrada MUST descrever o passo a passo de preparação do ambiente correspondente ao comportamento real do sistema, incluindo como a estrutura de dados é preparada.
- **FR-032**: A documentação de entrada MUST registrar as decisões de desenho que um leitor questionaria, com a justificativa de cada uma.
- **FR-033**: A documentação de entrada MUST NOT conter afirmação desatualizada nem descrição de estágio de desenvolvimento já superado.

### Key Entities

- **Venda**: registro existente cujo total geral passa a ser sempre exatamente igual à soma dos totais dos itens não cancelados, com valores expressos em no máximo duas casas decimais. Não muda de estrutura conceitual nesta feature.
- **Item de venda**: linha da venda cujos valores derivados — desconto e total — passam a ser arredondados no momento do cálculo. O produto que ele referencia passa a ocupar seu lugar na venda de forma permanente, mesmo após o cancelamento do item.
- **Estrutura de dados**: o conjunto de tabelas, colunas, índices e restrições que sustenta a persistência. Passa a ser provisionado como etapa própria do ambiente e a seguir uma convenção única de nomenclatura.
- **Resposta de erro**: contrato único de comunicação de falha ao solicitante, composto por chave e mensagem, que passa a cobrir também as falhas inesperadas.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A partir de um ambiente completamente limpo, uma pessoa sem contexto prévio consegue, seguindo apenas a documentação de entrada, executar um único comando de preparação e registrar uma venda com sucesso na primeira tentativa — sem nenhuma etapa manual intermediária.
- **SC-002**: 100% das vendas registradas apresentam total geral exatamente igual à soma dos totais dos seus itens não cancelados, incluindo os casos em que os cálculos de desconto produzem frações menores que um centavo.
- **SC-003**: 100% dos valores monetários devolvidos em uma operação de escrita são idênticos aos devolvidos na consulta subsequente do mesmo registro.
- **SC-004**: 100% das respostas de erro da API — de regra de negócio, de recurso não encontrado e de falha inesperada — seguem o mesmo formato, e nenhuma delas expõe detalhe interno de implementação.
- **SC-005**: 100% das tentativas de reintroduzir em uma venda um produto já presente, em qualquer estado, são recusadas como violação de regra de negócio, sem nenhuma ocorrência de falha inesperada.
- **SC-006**: 100% dos identificadores do modelo físico podem ser referenciados em consultas manuais sem delimitação especial, e correspondem aos nomes do modelo de dados documentado.
- **SC-007**: A suíte de verificações automatizadas permanece integralmente verde e a cobertura de testes se mantém acima do mínimo de 90% exigido pelo projeto, após todas as mudanças desta feature.
- **SC-008**: A compilação do projeto conclui sem nenhum aviso, e a inicialização da aplicação não registra nenhum aviso de configuração inconsistente.
- **SC-009**: Nenhum comportamento observável das seis operações já existentes é alterado por esta feature, exceto os explicitamente descritos nas User Stories 2, 3 e 4.

## Assumptions

- O ambiente de execução alvo desta feature é o ambiente de desenvolvimento e avaliação orquestrado por contêineres, já existente no projeto. A adequação da abordagem de provisionamento a um ambiente produtivo real é registrada como justificativa de desenho e documentada, mas nenhum ambiente produtivo é provisionado por esta feature.
- A convenção de nomenclatura adotada para o modelo físico é a já descrita no modelo de dados documentado do projeto — identificadores em minúsculas com separação por sublinhado —, e não uma convenção nova.
- A padronização de nomes do modelo físico é aplicada como evolução do modelo existente, preservando o histórico de evolução da estrutura de dados, e não como reescrita do estado inicial.
- A regra de unicidade de produto por venda passa a ser interpretada como abrangendo toda a vida da venda, incluindo itens cancelados. A alternativa — permitir a reintrodução de um produto cujo item foi cancelado — foi considerada e descartada por divergir da invariante como documentada e por exigir alteração da restrição de integridade que a sustenta no banco.
- A verificação de saúde continua sendo consumida por orquestradores e balanceadores como sinal de prontidão, e por isso mantém seu contrato de resposta atual mesmo passando a cobrir mais uma condição.
- As demais decisões de desenho já registradas no projeto — exclusão lógica, identidades externas denormalizadas, controle otimista de concorrência, despacho de eventos após a persistência — permanecem inalteradas e não são reavaliadas por esta feature.
- Nenhuma nova biblioteca de terceiros é introduzida por esta feature, preservando a stack tecnológica definida pela constitution do projeto sem necessidade de emenda.
- Estão explicitamente fora do escopo desta feature: novos casos de uso ou endpoints, autenticação e autorização, garantia de entrega de eventos por registro transacional de saída, representação de valor monetário como objeto de valor próprio com moeda, paginação por cursor e publicação de eventos em intermediário de mensagens real. São evoluções reconhecidas e já registradas como decisões conscientes do projeto, sem relação de dependência com o que esta feature entrega.
