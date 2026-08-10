# Feature Specification: Listar Vendas

**Feature Branch**: `004-listar-vendas`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "Feature 004: Listar vendas (UC-03 da documentação DDD no Notion). Como cliente da API, quero listar vendas de forma paginada (GET /api/sales), ordenadas por data da venda decrescente, para visualizar rapidamente o histórico de vendas sem precisar consultar cada uma individualmente e sem carregar o payload com os itens de cada venda. A listagem retorna a venda em forma resumida — identificador técnico, número da venda, data, cliente e filial denormalizados, total geral e situação de cancelamento — deliberadamente sem a coleção de itens, para manter a resposta leve quando há muitas vendas. A listagem aceita os parâmetros opcionais \"page\" (página solicitada, padrão 1) e \"pageSize\" (itens por página, padrão 20, limitado a 100) para controlar a paginação, e deve informar na resposta os metadados necessários para o cliente saber em que página está e quantas páginas existem no total (total de registros, total de páginas, página atual e tamanho de página). Também aceita os filtros opcionais \"customerId\" (identidade externa do cliente) e \"branchId\" (identidade externa da filial), que restringem o resultado às vendas daquele cliente ou filial. Aceita ainda o filtro opcional \"isCancelled\": quando informado, retorna somente vendas ativas ou somente vendas canceladas conforme o valor; quando omitido, a listagem traz vendas ativas e canceladas juntas, sem distinção — cancelamento é um estado do registro, não uma remoção, e a venda cancelada continua visível no histórico. Quando nenhuma venda atender aos filtros informados (ou quando a página solicitada estiver além do total de páginas existentes), o sistema deve responder com sucesso e uma lista vazia, nunca com erro de recurso não encontrado — lista vazia é um resultado válido, diferente de identificador inexistente. Quando os parâmetros de paginação ou filtro forem inválidos (por exemplo, \"page\" ou \"pageSize\" menores que 1, \"pageSize\" acima do limite máximo, ou \"customerId\"/\"branchId\"/\"isCancelled\" em formato que não corresponde ao tipo esperado), o sistema deve responder com erro de requisição malformada, seguindo o mesmo contrato de erro (Result/Notification traduzido para HTTP) usado nos demais casos de uso. Este é um caso de uso somente leitura (Query): não deve alterar nenhum estado de nenhuma venda, não deve revalidar nem reprocessar regras de negócio de registro (desconto, limites de quantidade etc. — já aplicadas e persistidas no momento do registro ou de alterações anteriores), e não deve disparar nenhum evento de domínio. Os dados de cliente e filial retornados em cada venda da lista já vêm denormalizados (identificador e nome), sem exigir consulta a nenhum outro serviço para montar a listagem."

## Clarifications

### Session 2026-08-09

- Q: Quando duas ou mais vendas têm exatamente a mesma data (`saleDate`), qual critério de desempate garante que a ordem entre elas seja sempre a mesma nas páginas seguintes? → A: Desempate pelo identificador técnico (`Id`) da venda

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Listar vendas paginadas em ordem cronológica (Priority: P1)

Um sistema cliente (front-end, PDV ou BFF) solicita a lista de vendas registradas, sem informar filtros, e recebe de volta uma página de vendas em forma resumida — sem os itens — ordenadas da mais recente para a mais antiga, junto com os metadados necessários para navegar pelas páginas seguintes.

**Why this priority**: É o núcleo do caso de uso — sem essa capacidade não existe visão geral do histórico de vendas, apenas consultas pontuais por identificador (feature 003). É o que viabiliza uma tela de listagem.

**Independent Test**: Pode ser testada isoladamente registrando várias vendas, solicitando a listagem sem filtros e verificando que a página retornada respeita o tamanho padrão, a ordenação por data decrescente e que cada venda vem sem a coleção de itens.

**Acceptance Scenarios**:

1. **Given** várias vendas registradas em datas diferentes, **When** o solicitante lista as vendas sem informar parâmetros, **Then** o sistema responde com status 200, a primeira página (até 20 vendas) ordenada da data mais recente para a mais antiga, e os metadados de paginação (total de registros, total de páginas, página atual e tamanho de página) preenchidos corretamente.
2. **Given** uma listagem de vendas, **When** o solicitante inspeciona um item da lista, **Then** cada venda traz identificador técnico, número da venda, data, cliente e filial denormalizados (identificador e nome), total geral e situação de cancelamento — sem a coleção de itens.
3. **Given** mais vendas registradas do que cabem em uma página, **When** o solicitante informa "page" e "pageSize" explicitamente, **Then** o sistema retorna exatamente as vendas correspondentes àquela página, respeitando o tamanho solicitado até o limite máximo permitido.

---

### User Story 2 - Filtrar vendas por cliente, filial e situação de cancelamento (Priority: P2)

Um sistema cliente solicita a listagem de vendas restrita a um cliente específico, a uma filial específica, e/ou a uma situação de cancelamento (somente ativas ou somente canceladas), para montar visões específicas como "vendas do cliente X" ou "vendas canceladas da filial Y".

**Why this priority**: Filtros tornam a listagem útil em cenários reais de consulta segmentada, mas dependem da capacidade básica de listar (US1) já existir. Sem eles, a listagem ainda entrega valor, só que menos direcionado.

**Independent Test**: Pode ser testada isoladamente registrando vendas de clientes, filiais e situações de cancelamento diferentes, aplicando cada filtro isoladamente e em combinação, e verificando que somente as vendas correspondentes aparecem no resultado.

**Acceptance Scenarios**:

1. **Given** vendas de múltiplos clientes, **When** o solicitante lista vendas informando "customerId", **Then** o sistema retorna apenas as vendas daquele cliente, com os demais metadados de paginação recalculados sobre o subconjunto filtrado.
2. **Given** vendas de múltiplas filiais, **When** o solicitante lista vendas informando "branchId", **Then** o sistema retorna apenas as vendas daquela filial.
3. **Given** vendas ativas e canceladas, **When** o solicitante lista vendas informando "isCancelled" como verdadeiro ou falso, **Then** o sistema retorna somente as vendas com a situação de cancelamento correspondente.
4. **Given** vendas ativas e canceladas, **When** o solicitante lista vendas sem informar "isCancelled", **Then** o sistema retorna vendas ativas e canceladas juntas, sem distinção.
5. **Given** filtros de cliente, filial e situação de cancelamento informados simultaneamente, **When** a listagem é solicitada, **Then** o sistema retorna apenas as vendas que atendem a todos os filtros informados ao mesmo tempo.

---

### User Story 3 - Lidar com listagens sem resultado e parâmetros inválidos (Priority: P3)

Um sistema cliente solicita a listagem com filtros que não correspondem a nenhuma venda, ou com uma página além do total existente, e recebe uma lista vazia com sucesso; ou solicita a listagem com parâmetros de paginação ou filtro em formato inválido, e recebe uma resposta clara de requisição malformada.

**Why this priority**: Trata-se de casos de borda importantes para a robustez da integração, mas não bloqueiam o valor central de listar e filtrar vendas existentes (US1 e US2). É a menor prioridade por afetar apenas caminhos de exceção e de resultado vazio.

**Independent Test**: Pode ser testada isoladamente solicitando a listagem com um filtro que não corresponde a nenhuma venda (ou uma página muito além do total) e confirmando lista vazia com status de sucesso; e separadamente enviando parâmetros inválidos (página ou tamanho de página não positivos, tamanho de página acima do limite, identificadores ou flag em formato inesperado) e confirmando resposta de requisição malformada.

**Acceptance Scenarios**:

1. **Given** um filtro que não corresponde a nenhuma venda registrada, **When** a listagem é solicitada, **Then** o sistema responde com status 200 e uma lista vazia, com os metadados de paginação indicando zero registros.
2. **Given** uma página solicitada além do total de páginas existentes, **When** a listagem é solicitada, **Then** o sistema responde com status 200 e uma lista vazia, sem erro de recurso não encontrado.
3. **Given** um valor de "page" ou "pageSize" menor que 1, **When** a listagem é solicitada, **Then** o sistema responde com status 400 e uma mensagem indicando o parâmetro inválido, no mesmo formato de erro usado pelos demais casos de uso.
4. **Given** um "pageSize" acima do limite máximo permitido, **When** a listagem é solicitada, **Then** o sistema responde com status 400 indicando que o valor excede o limite.
5. **Given** um "customerId", "branchId" ou "isCancelled" em formato que não corresponde ao tipo esperado, **When** a listagem é solicitada, **Then** o sistema responde com status 400 indicando o parâmetro inválido.

---

### Edge Cases

- O que acontece quando não existe nenhuma venda registrada no sistema e a listagem é solicitada sem filtros?
- Como o sistema se comporta quando "customerId" e "branchId" são informados juntos e não existe nenhuma venda que atenda a ambos simultaneamente?
- O que acontece quando o "pageSize" informado é exatamente igual ao limite máximo permitido?
- Como o sistema se comporta quando a última página tem menos vendas do que o tamanho de página solicitado?
- Quando duas vendas têm exatamente a mesma data (mesmo instante), a ordenação entre elas usa o identificador técnico como desempate, garantindo resultado estável e previsível entre páginas.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir listar vendas de forma paginada através de um endpoint de consulta, aceitando os parâmetros opcionais "page" (padrão 1) e "pageSize" (padrão 20).
- **FR-002**: O sistema MUST limitar "pageSize" a um máximo de 100 registros por página, rejeitando como requisição malformada qualquer valor acima desse limite.
- **FR-003**: O sistema MUST ordenar o resultado da listagem por data da venda, da mais recente para a mais antiga, usando o identificador técnico da venda como critério de desempate para vendas com a mesma data, garantindo ordem determinística entre páginas.
- **FR-004**: O sistema MUST retornar, para cada venda listada, uma representação resumida contendo identificador técnico, número da venda, data, cliente e filial denormalizados (identificador e nome), total geral e situação de cancelamento — sem a coleção de itens da venda.
- **FR-005**: O sistema MUST incluir na resposta os metadados de paginação: total de registros, total de páginas, página atual e tamanho de página.
- **FR-006**: O sistema MUST permitir filtrar o resultado pela identidade externa do cliente ("customerId"), quando informada.
- **FR-007**: O sistema MUST permitir filtrar o resultado pela identidade externa da filial ("branchId"), quando informada.
- **FR-008**: O sistema MUST permitir filtrar o resultado pela situação de cancelamento ("isCancelled"), retornando apenas vendas ativas ou apenas vendas canceladas conforme o valor informado; quando o filtro não for informado, o sistema MUST retornar vendas ativas e canceladas juntas.
- **FR-009**: O sistema MUST combinar múltiplos filtros informados simultaneamente de forma cumulativa, retornando apenas as vendas que atendem a todos os filtros ao mesmo tempo.
- **FR-010**: O sistema MUST responder com sucesso e uma lista vazia quando nenhuma venda atender aos filtros informados, ou quando a página solicitada estiver além do total de páginas existentes — nunca com erro de recurso não encontrado.
- **FR-011**: O sistema MUST responder com erro de requisição malformada quando "page" ou "pageSize" forem menores que 1, quando "pageSize" exceder o limite máximo, ou quando "customerId", "branchId" ou "isCancelled" estiverem em formato que não corresponde ao tipo esperado.
- **FR-012**: O sistema MUST seguir, nas respostas de erro, o mesmo contrato de erro (Result/Notification traduzido para HTTP) usado pelos demais casos de uso da API.
- **FR-013**: O sistema MUST tratar a listagem como uma operação somente leitura, sem alterar qualquer estado de nenhuma venda.
- **FR-014**: O sistema MUST NOT reprocessar ou revalidar regras de negócio de registro (cálculo de desconto, limites de quantidade etc.) durante a listagem — apenas retornar os valores já calculados e persistidos.
- **FR-015**: O sistema MUST NOT disparar nenhum evento de domínio como resultado de uma listagem.
- **FR-016**: O sistema MUST retornar os dados de cliente e filial já denormalizados (identificador e nome) em cada venda listada, sem exigir consulta a nenhum outro serviço.

### Key Entities

- **Venda (forma resumida)**: representação enxuta de uma venda já registrada, usada exclusivamente na listagem — identificador técnico, número de negócio, data, cliente e filial denormalizados, total geral e situação de cancelamento, deliberadamente sem a coleção de itens.
- **Cliente e Filial**: entidades pertencentes a outros domínios, usadas tanto como filtro (por identificador) quanto retornadas denormalizadas (identificador e nome) em cada venda da lista.
- **Página de resultados**: conjunto de vendas resumidas correspondente aos parâmetros de paginação solicitados, acompanhado dos metadados de total de registros, total de páginas, página atual e tamanho de página.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das solicitações de listagem sem filtros retornam vendas em forma resumida, ordenadas por data decrescente, com os metadados de paginação corretos para o total de registros existente.
- **SC-002**: 100% das solicitações de listagem com filtros de cliente, filial e/ou situação de cancelamento retornam exclusivamente as vendas que atendem a todos os filtros informados.
- **SC-003**: 100% das solicitações sem vendas correspondentes (por filtro ou por página além do total) recebem uma lista vazia com sucesso, nunca uma resposta de erro.
- **SC-004**: 100% das solicitações com parâmetros de paginação ou filtro inválidos recebem uma resposta de requisição malformada, no mesmo contrato de erro usado pelos demais casos de uso.
- **SC-005**: 0% das solicitações de listagem alteram qualquer dado de venda ou disparam eventos de domínio.
- **SC-006**: O solicitante consegue montar uma tela de listagem completa (itens da página, navegação entre páginas e filtros aplicados) a partir de uma única resposta, sem depender de chamadas a outros domínios ou de consultas adicionais por venda.

## Assumptions

- O tamanho de página padrão é 20 e o máximo permitido é 100; valores acima do máximo são tratados como requisição inválida (400), não como um valor a ser silenciosamente limitado ao teto.
- A ordenação é fixa por data da venda decrescente nesta feature; ordenação customizável por outros campos pertence a uma feature futura.
- A listagem nunca retorna a coleção de itens de cada venda; para obter os itens de uma venda específica, o cliente utiliza a consulta individual (feature 003 — GET /api/sales/{id}).
- Autenticação e autorização do solicitante estão fora do escopo desta feature, assim como nas demais features da API.
- Quando mais de um filtro é informado (cliente, filial, situação de cancelamento), eles se combinam de forma cumulativa (E lógico), não alternativa.
- Alteração e cancelamento de vendas e de itens são especificados em features futuras e não fazem parte desta listagem.
