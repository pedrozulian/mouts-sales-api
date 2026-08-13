# Feature Specification: Release Automatizado e Publicação de Imagens

**Feature Branch**: `009-release-e-publicacao`

**Created**: 2026-08-12

**Status**: Draft

**Input**: User description: "Release automatizado e publicação de imagens Docker. Escopo: (1) versionamento semântico automatizado a partir dos conventional commits, gerando tag e CHANGELOG.md; (2) pipeline de CD que publica duas imagens no Docker Hub — a API e o migrator — versionadas pela mesma tag, linux/amd64; (3) endurecimento da imagem para consumo externo: ASPNETCORE_ENVIRONMENT=Production explícito como default e falha explícita no startup quando a connection string não for fornecida; (4) docker-compose de release consumindo as imagens publicadas via image: em vez de build:; (5) smoke test do migrator publicado contra um PostgreSQL efêmero dentro do workflow de release; (6) conformidade com o Princípio IV da constitution: renomear os identificadores em português produtoJaPertenceAVenda (src/SalesApi.Domain/Sales/Sale.cs:245), produtoNovo e quantidadeInvalida (tests/SalesApi.Domain.Tests/Sales/SaleTests.cs), remover comentários desnecessários, e emendar a constitution para explicitar que nomes de método de teste podem permanecer em português por serem documentação executável de comportamento."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Executar o sistema sem construí-lo (Priority: P1)

Uma pessoa que quer avaliar ou operar o sistema obtém os artefatos executáveis já prontos a partir de um registro público, informa apenas o endereço e as credenciais do banco de dados que ela mesma escolheu, e coloca o sistema no ar — sem clonar o repositório, sem instalar ferramentas de compilação e sem construir nada localmente.

**Why this priority**: É o que transforma o repositório em algo entregável. Hoje a única forma de executar o sistema é a partir do código-fonte, com uma etapa de construção local que leva minutos e exige ferramentas específicas. Enquanto isso não existir, nenhuma das demais histórias tem sobre o que operar — todas elas versionam, verificam ou documentam artefatos que ainda não são publicados.

**Independent Test**: Pode ser testada isoladamente em uma máquina que nunca teve contato com o repositório, contendo apenas um runtime de contêineres, obtendo os artefatos publicados, apontando-os para um banco de dados vazio e registrando uma venda com sucesso.

**Acceptance Scenarios**:

1. **Given** uma máquina sem o código-fonte do projeto e sem ferramentas de compilação, **When** os artefatos publicados são obtidos e executados apontando para um banco de dados acessível, **Then** o sistema fica operacional e o registro de uma venda é concluído com sucesso.
2. **Given** um banco de dados vazio, sem nenhuma estrutura criada, **When** o artefato de preparação da estrutura de dados é executado contra ele, **Then** a estrutura é criada por completo e o artefato encerra sinalizando sucesso.
3. **Given** os artefatos publicados, **When** eles são obtidos, **Then** cada um deles é identificável por uma versão explícita, e existe uma versão que corresponde reconhecidamente à mais recente publicada.
4. **Given** o artefato da aplicação e o artefato de preparação da estrutura de dados de uma mesma versão, **When** ambos são executados contra o mesmo banco, **Then** a aplicação reporta-se saudável, sem divergência entre a estrutura esperada por ela e a estrutura efetivamente criada.
5. **Given** um banco de dados hospedado em máquina distinta da que executa a aplicação, **When** o sistema é colocado no ar apontando para ele, **Then** funciona sem nenhuma alteração nos artefatos — apenas com a informação de conexão fornecida externamente.

---

### User Story 2 - Configuração fornecida por quem implanta, com falha imediata e legível (Priority: P1)

Uma pessoa que executa os artefatos publicados sem fornecer a informação de conexão com o banco de dados recebe, imediatamente na inicialização, uma mensagem que diz exatamente o que faltou e como fornecer — em vez de o sistema subir aparentemente bem e falhar depois, de forma obscura, na primeira operação.

**Why this priority**: É a diferença entre um artefato utilizável e um artefato que só funciona para quem o construiu. Hoje a ausência da informação de conexão não é detectada na inicialização; o sistema sobe e a falha só aparece na primeira operação de banco, como erro de infraestrutura sem relação aparente com configuração ausente. Além disso, a configuração de conveniência voltada ao desenvolvimento local está embutida nos artefatos e pode ser aplicada silenciosamente fora do contexto para o qual foi criada. Ambos os defeitos se manifestam exatamente no primeiro contato de alguém externo com o sistema.

**Independent Test**: Pode ser testada isoladamente executando o artefato da aplicação sem fornecer nenhuma informação de conexão e verificando que ele encerra imediatamente com mensagem que nomeia o que faltou — e, em seguida, fornecendo a informação e verificando que ele opera normalmente.

**Acceptance Scenarios**:

1. **Given** o artefato da aplicação, **When** ele é executado sem que a informação de conexão com o banco de dados seja fornecida, **Then** a inicialização é interrompida imediatamente com mensagem que identifica qual configuração está ausente e como fornecê-la.
2. **Given** o artefato da aplicação, **When** ele é executado sem informação de conexão, **Then** ele não recorre silenciosamente a nenhum endereço de banco de dados predefinido.
3. **Given** o artefato da aplicação, **When** ele é executado sem que o perfil de comportamento seja explicitamente informado, **Then** ele opera no perfil mais restritivo, adequado a ambiente produtivo.
4. **Given** o artefato da aplicação, **When** quem o executa deseja o perfil de desenvolvimento, **Then** consegue obtê-lo informando-o explicitamente, sem precisar de um artefato diferente.
5. **Given** um mesmo artefato de uma dada versão, **When** ele é executado em contextos distintos — avaliação local e ambiente produtivo —, **Then** é exatamente o mesmo artefato em ambos, diferindo apenas na configuração fornecida de fora.

---

### User Story 3 - Cada versão publicada tem histórico legível e rastreável (Priority: P2)

Uma pessoa que acompanha o projeto identifica, para qualquer versão publicada, o que mudou em relação à anterior, agrupado por natureza da mudança — sem precisar ler o histórico de commits e sem depender de alguém ter lembrado de escrever esse resumo à mão.

**Why this priority**: Dá significado à numeração das versões: sem histórico, uma versão é apenas um rótulo arbitrário. O histórico é derivado automaticamente do registro de mudanças que o projeto já mantém de forma disciplinada, o que elimina o risco de divergência entre o que foi feito e o que foi anunciado. Depende de existir publicação versionada (User Story 1) para ter o que descrever.

**Independent Test**: Pode ser testada isoladamente registrando mudanças de naturezas distintas, disparando a preparação de uma nova versão e verificando que o histórico gerado contém todas as mudanças relevantes, corretamente agrupadas, e que a versão foi incrementada conforme a natureza da mudança mais significativa.

**Acceptance Scenarios**:

1. **Given** um conjunto de mudanças incorporadas desde a última versão publicada, **When** uma nova versão é preparada, **Then** o histórico é gerado automaticamente a partir do registro dessas mudanças, agrupado por natureza.
2. **Given** mudanças que incluem ao menos uma adição de funcionalidade, **When** a nova versão é determinada, **Then** o incremento de versão reflete a natureza mais significativa entre as mudanças incorporadas.
3. **Given** uma nova versão preparada, **When** ela é efetivada, **Then** a versão passa a existir como marco permanente no histórico do projeto, e a publicação dos artefatos correspondentes é disparada por esse marco.
4. **Given** o histórico de versões, **When** ele é consultado, **Then** cada versão publicada aparece nele, e a versão dos artefatos publicados corresponde exatamente à versão descrita.
5. **Given** mudanças que não alteram comportamento observável do sistema, **When** a nova versão é determinada, **Then** elas constam do histórico sem provocar incremento de versão desproporcional à sua natureza.

---

### User Story 4 - Publicação só ocorre se o artefato realmente funcionar (Priority: P2)

Uma pessoa que obtém uma versão publicada tem a garantia de que aquele artefato específico foi executado com sucesso antes de ser disponibilizado — e não apenas de que o código que o originou passou nas verificações.

**Why this priority**: Verificar o código-fonte não é a mesma coisa que verificar o artefato produzido a partir dele. Há classes inteiras de defeito que só aparecem no artefato final — incompatibilidade de arquitetura, arquivo ausente na montagem, ponto de entrada incorreto — e que passariam despercebidas por todas as verificações atuais, chegando ao usuário como um artefato que simplesmente não executa. Depende da User Story 1 para existir artefato a verificar.

**Independent Test**: Pode ser testada isoladamente introduzindo deliberadamente um defeito que só se manifeste no artefato montado e verificando que a publicação é interrompida antes que ele seja disponibilizado.

**Acceptance Scenarios**:

1. **Given** os artefatos recém-produzidos de uma versão, **When** a publicação é processada, **Then** o artefato de preparação da estrutura de dados é efetivamente executado contra um banco de dados descartável antes de a versão ser considerada publicada.
2. **Given** um artefato que falha ao ser executado, **When** a verificação ocorre, **Then** a publicação é interrompida e a versão não é disponibilizada.
3. **Given** a verificação do artefato, **When** ela é executada, **Then** não depende de nenhum banco de dados preexistente nem deixa qualquer resíduo após concluir.
4. **Given** uma verificação bem-sucedida, **When** ela conclui, **Then** a estrutura de dados criada por ela corresponde à esperada pela aplicação da mesma versão.

---

### User Story 5 - Base de código em conformidade com a convenção de idioma (Priority: P3)

Uma pessoa que revisa o código encontra identificadores exclusivamente no idioma definido pela convenção do projeto, e comentários que explicam apenas o que não é dedutível do próprio código.

**Why this priority**: São desvios pontuais de uma convenção que o projeto define formalmente e cumpre em praticamente toda a sua extensão. Não causam defeito algum, mas são exatamente o tipo de inconsistência que quem revisa nota, e que enfraquece a credibilidade das demais convenções declaradas. É a menor prioridade por não ter relação de dependência com nenhuma outra história nem impacto em comportamento.

**Independent Test**: Pode ser testada isoladamente inspecionando a base de código em busca de identificadores fora do idioma da convenção e verificando que a suíte permanece integralmente verde após as renomeações.

**Acceptance Scenarios**:

1. **Given** a base de código de produção, **When** ela é inspecionada, **Then** todos os identificadores — classes, métodos, variáveis, parâmetros e namespaces — estão no idioma definido pela convenção do projeto.
2. **Given** a base de código de testes, **When** ela é inspecionada, **Then** todos os identificadores de variável e parâmetro estão no idioma da convenção, e os nomes de método de teste seguem a regra explicitamente definida para eles.
3. **Given** os comentários existentes na base de código, **When** eles são revisados, **Then** permanecem apenas os que explicam decisão, restrição ou motivo não dedutível do código, e os que apenas repetem o que o código já expressa são removidos.
4. **Given** as renomeações aplicadas, **When** a suíte de verificações é executada, **Then** ela permanece integralmente verde, sem nenhuma alteração de comportamento.
5. **Given** o documento de princípios do projeto, **When** ele é comparado à base de código, **Then** não existe regra declarada que a base de código viole de forma visível.

---

### Edge Cases

- O que acontece quando a publicação de uma versão é disparada mas o registro público de artefatos está indisponível ou recusa as credenciais?
- Como o sistema se comporta quando a informação de conexão é fornecida mas está sintaticamente inválida ou aponta para um banco inacessível — a falha é distinguível da ausência de configuração?
- O que acontece quando alguém executa o artefato da aplicação contra um banco cuja estrutura foi preparada por uma versão diferente da sua?
- Como a numeração de versões se comporta na primeira publicação, quando não existe versão anterior com que comparar?
- O que acontece quando duas preparações de versão são disparadas em sequência rápida, antes que a primeira conclua?
- O que acontece quando a verificação do artefato falha depois que parte dos artefatos daquela versão já foi disponibilizada?
- Como quem obtém os artefatos distingue uma versão específica da referência à mais recente, e o que acontece quando a mais recente é republicada?
- O que acontece quando o perfil de comportamento é informado com um valor não reconhecido?

## Requirements *(mandatory)*

### Functional Requirements

**Publicação de artefatos executáveis**

- **FR-001**: O projeto MUST publicar, em um registro público de artefatos, os executáveis necessários para colocar o sistema no ar sem acesso ao código-fonte.
- **FR-002**: O projeto MUST publicar dois artefatos distintos: o da aplicação e o de preparação da estrutura de dados, de modo que quem os obtém consiga tanto criar a estrutura em um banco vazio quanto executar a aplicação.
- **FR-003**: Os dois artefatos de uma mesma publicação MUST ser identificados pela mesma versão, tornando inequívoca a correspondência entre eles.
- **FR-004**: Cada artefato publicado MUST ser identificável tanto por sua versão específica quanto por uma referência à publicação mais recente.
- **FR-005**: A publicação MUST ser disparada exclusivamente pelo marco de uma nova versão, e MUST NOT ocorrer a cada mudança incorporada ao projeto.
- **FR-006**: A publicação MUST ser interrompida sem disponibilizar nenhum artefato quando qualquer etapa de verificação ou de produção dos artefatos falhar.
- **FR-007**: As credenciais de acesso ao registro público MUST ser fornecidas ao processo de publicação de forma protegida, e MUST NOT constar do código-fonte nem de qualquer artefato publicado.

**Configuração e comportamento dos artefatos**

- **FR-008**: O artefato da aplicação MUST obter a informação de conexão com o banco de dados exclusivamente do ambiente em que é executado, e MUST NOT embutir endereço, credencial ou identificação de banco específico.
- **FR-009**: O artefato da aplicação MUST interromper a inicialização imediatamente quando a informação de conexão não for fornecida, com mensagem que identifique a configuração ausente e a forma de fornecê-la.
- **FR-010**: O artefato da aplicação MUST NOT recorrer a qualquer endereço de banco de dados predefinido quando a informação de conexão estiver ausente.
- **FR-011**: O artefato da aplicação MUST adotar, na ausência de indicação explícita, o perfil de comportamento mais restritivo, adequado a ambiente produtivo.
- **FR-012**: O perfil de comportamento MUST poder ser alterado por quem executa o artefato, sem exigir um artefato diferente.
- **FR-013**: O artefato de preparação da estrutura de dados MUST aceitar a informação de conexão tanto a partir do ambiente quanto como parâmetro explícito de execução, prevalecendo o parâmetro explícito quando ambos forem fornecidos.
- **FR-014**: O artefato de preparação da estrutura de dados MUST encerrar a execução sinalizando de forma inequívoca se a preparação foi concluída com sucesso ou falhou.
- **FR-015**: O mesmo artefato de uma dada versão MUST ser utilizável em qualquer contexto de execução, sendo a configuração fornecida externamente a única diferença entre eles.

**Versionamento e histórico**

- **FR-016**: O projeto MUST determinar automaticamente a nova versão a partir da natureza das mudanças incorporadas desde a versão anterior, seguindo versionamento semântico.
- **FR-017**: O projeto MUST gerar automaticamente o histórico de mudanças de cada versão, agrupado por natureza da mudança, sem redação manual.
- **FR-018**: O histórico de mudanças MUST ser mantido no repositório como documento acumulativo, preservando as versões anteriores.
- **FR-019**: Cada versão publicada MUST existir como marco permanente no histórico do projeto, e esse marco MUST ser o gatilho da publicação dos artefatos correspondentes.
- **FR-020**: A versão que identifica os artefatos publicados MUST corresponder exatamente à versão descrita no histórico de mudanças.
- **FR-021**: A preparação de uma nova versão MUST ser revisável antes de ser efetivada, permitindo verificar a versão determinada e o histórico gerado.

**Verificação do artefato publicado**

- **FR-022**: O processo de publicação MUST executar o artefato de preparação da estrutura de dados contra um banco de dados descartável antes de considerar a versão publicada.
- **FR-023**: Essa verificação MUST NOT depender de nenhum banco de dados preexistente nem deixar resíduo após concluir.
- **FR-024**: A falha dessa verificação MUST interromper a publicação.

**Execução a partir dos artefatos publicados**

- **FR-025**: O projeto MUST oferecer uma forma documentada de colocar o sistema completo no ar a partir dos artefatos publicados, sem construção local.
- **FR-026**: Essa forma MUST preservar a ordem de provisionamento já estabelecida pelo projeto — a estrutura de dados preparada e concluída antes de a aplicação atender requisições.
- **FR-027**: A forma de execução a partir do código-fonte MUST continuar existindo e funcionando, sem ser substituída pela forma baseada em artefatos publicados.

**Documentação**

- **FR-028**: A documentação de entrada MUST descrever como obter e executar os artefatos publicados, incluindo qual configuração precisa ser fornecida e como fornecê-la.
- **FR-029**: A documentação de entrada MUST apresentar a execução a partir dos artefatos publicados e a execução a partir do código-fonte como alternativas, deixando claro quando cada uma se aplica.
- **FR-030**: A documentação de entrada MUST indicar onde consultar o histórico de mudanças e as versões disponíveis.

**Conformidade com a convenção de idioma**

- **FR-031**: Todos os identificadores da base de código de produção MUST estar no idioma definido pela convenção do projeto para identificadores.
- **FR-032**: Todos os identificadores de variável e parâmetro da base de código de testes MUST estar no idioma definido pela convenção do projeto para identificadores.
- **FR-033**: O documento de princípios do projeto MUST declarar explicitamente a regra aplicável aos nomes de método de teste, eliminando a ambiguidade sobre se eles são identificadores de código ou documentação de comportamento.
- **FR-034**: Os comentários da base de código MUST se limitar aos que explicam decisão, restrição ou motivo não dedutível do código; os que apenas reafirmam o que o código já expressa MUST ser removidos.
- **FR-035**: O documento de princípios do projeto MUST ser emendado para refletir as adições à stack tecnológica introduzidas por esta feature, com o versionamento e o registro de emenda que ele próprio exige.
- **FR-036**: As mudanças de conformidade MUST NOT alterar nenhum comportamento observável do sistema.

### Key Entities

- **Artefato executável**: unidade autocontida que executa sem exigir código-fonte ou ferramentas de construção. Existem dois nesta feature — o da aplicação, que atende requisições, e o de preparação da estrutura de dados, que executa uma vez e encerra. Ambos são identificados por versão e são agnósticos ao ambiente em que rodam.
- **Versão**: identificador semântico derivado automaticamente da natureza das mudanças incorporadas. Vincula, de forma inequívoca, um conjunto de artefatos publicados a um trecho do histórico de mudanças e a um marco no histórico do projeto.
- **Histórico de mudanças**: documento acumulativo, gerado automaticamente, que descreve o que mudou em cada versão, agrupado por natureza da mudança.
- **Configuração de ambiente**: conjunto de informações fornecidas de fora do artefato no momento da execução — informação de conexão com o banco de dados e perfil de comportamento. É o que permite que o mesmo artefato sirva a contextos distintos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma pessoa em uma máquina sem o código-fonte e sem ferramentas de compilação consegue, seguindo apenas a documentação de entrada, colocar o sistema no ar a partir dos artefatos publicados e registrar uma venda com sucesso na primeira tentativa.
- **SC-002**: 100% das versões publicadas disponibilizam ambos os artefatos identificados pela mesma versão, sem nenhuma ocorrência de publicação parcial.
- **SC-003**: 100% das tentativas de executar o artefato da aplicação sem informação de conexão resultam em interrupção imediata da inicialização com mensagem que nomeia a configuração ausente, sem nenhuma ocorrência de falha tardia ou obscura.
- **SC-004**: 100% dos artefatos publicados tiveram o artefato de preparação da estrutura de dados executado com sucesso contra um banco descartável antes da disponibilização.
- **SC-005**: 100% das versões publicadas constam do histórico de mudanças, com a versão dos artefatos correspondendo exatamente à versão descrita.
- **SC-006**: O histórico de mudanças de cada versão é produzido sem nenhuma redação manual, e não apresenta divergência em relação às mudanças efetivamente incorporadas.
- **SC-007**: 100% dos identificadores da base de código estão no idioma definido pela convenção, e não existe regra declarada no documento de princípios que a base de código viole.
- **SC-008**: A suíte de verificações automatizadas permanece integralmente verde e a cobertura de testes se mantém acima do mínimo de 90% exigido pelo projeto, após todas as mudanças desta feature.
- **SC-009**: Nenhum comportamento observável das seis operações já existentes é alterado por esta feature, exceto o descrito na User Story 2 quanto à ausência de configuração obrigatória.
- **SC-010**: A forma de execução a partir do código-fonte continua funcionando exatamente como antes desta feature.

## Assumptions

- O registro público de artefatos alvo é o Docker Hub, sob conta pessoal do autor do projeto. A conta e as credenciais de publicação são provisionadas manualmente e registradas de forma protegida no ambiente de automação antes da primeira publicação — esta feature não automatiza a criação da conta.
- Os artefatos são publicados para uma única arquitetura de processador, correspondente à dos ambientes de automação e de avaliação. Publicação para múltiplas arquiteturas foi considerada e descartada: a produção do artefato de preparação da estrutura de dados exige montagem específica por arquitetura, e fazê-lo por emulação multiplicaria o tempo de publicação sem benefício para o público-alvo desta entrega.
- O escopo desta feature encerra-se na disponibilização de artefatos versionados e verificados — entrega contínua. A implantação automática em um ambiente produtivo real está fora do escopo, por não existir ambiente produtivo provisionado para este projeto. A distinção entre as duas coisas é registrada na documentação como decisão consciente.
- O registro de mudanças do projeto já segue, de forma disciplinada e desde o primeiro commit, uma convenção estruturada que permite derivar automaticamente tanto o incremento de versão quanto o histórico. Nenhuma mudança nessa prática é exigida por esta feature.
- A primeira versão publicada por esta feature parte da numeração inicial de um projeto que ainda não teve versões publicadas, e não tenta reconstruir versões retroativas para as entregas anteriores. O histórico de mudanças inicial pode, contudo, contemplar as mudanças já incorporadas ao projeto.
- A verificação do artefato publicado cobre o artefato de preparação da estrutura de dados por ser onde se concentram os modos de falha específicos de montagem. A verificação equivalente para o artefato da aplicação é considerada coberta pelas verificações automatizadas já existentes, que exercem a aplicação de ponta a ponta contra banco real.
- A documentação interativa da API permanece disponível também no perfil de comportamento produtivo, por decisão consciente relativa ao propósito de avaliação deste projeto, e essa decisão é registrada na documentação de entrada.
- A adição do registro público de artefatos e da ferramenta de automação de versionamento constitui ampliação da stack tecnológica definida pelo documento de princípios do projeto, exigindo emenda formal com incremento de versão desse documento — o que está previsto no escopo desta feature.
- Os nomes de método de teste permanecem no idioma da documentação, por funcionarem como descrição executável de comportamento esperado e não como interface do sistema. A alternativa — traduzi-los para o idioma dos identificadores — foi considerada e descartada por produzir alteração extensa sem ganho funcional, em detrimento da legibilidade das intenções de teste.
- As mensagens de erro devolvidas pela API permanecem no idioma da documentação, por serem comunicação dirigida a pessoas, e não identificadores de código. Nenhuma delas é alterada por esta feature.
- Estão explicitamente fora do escopo desta feature: novos casos de uso ou endpoints, autenticação e autorização, implantação automática em ambiente produtivo, publicação para múltiplas arquiteturas de processador, publicação em registro adicional além do escolhido, gestão de segredos por cofre dedicado e política de retenção ou expurgo de versões antigas.
