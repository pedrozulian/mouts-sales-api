# Feature Specification: Consultar Venda

**Feature Branch**: `003-consultar-venda`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "Feature 003: Consultar venda (UC-02 da documentação DDD no Notion). Como cliente da API, quero consultar uma venda específica pelo seu identificador técnico (GET /api/sales/{id}) para obter a representação completa do registro — incluindo os dados denormalizados de cliente, filial e produtos, todos os itens da venda (ativos e cancelados) e os valores de desconto e total já calculados — sem precisar consultar nenhum outro serviço. Uma venda cancelada deve ser retornada normalmente, com isCancelled verdadeiro: cancelamento é um estado do registro, não uma remoção, e o histórico continua acessível via consulta. Da mesma forma, cada item da venda deve expor seu próprio isCancelled, permitindo distinguir itens ativos de itens cancelados individualmente. Quando o identificador informado não corresponder a nenhuma venda existente, o sistema deve responder com 404 e uma mensagem clara de recurso não encontrado, seguindo o mesmo contrato de erro (Result/Notification traduzido para HTTP) usado nos demais casos de uso. Este é um caso de uso somente leitura (Query no MediatR): não deve alterar nenhum estado, não deve validar nem re-processar regras de negócio de registro (desconto, limites de quantidade etc. — esses já foram aplicados e persistidos no momento do registro), e não deve disparar nenhum evento de domínio."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consultar uma venda existente pelo identificador (Priority: P1)

Um sistema cliente (front-end, PDV ou BFF) consulta uma venda específica informando o identificador técnico retornado no momento do registro, e recebe de volta a representação completa da venda: cliente, filial, itens, descontos e totais já calculados — sem precisar consultar nenhum outro serviço para montar a tela ou o comprovante.

**Why this priority**: É o núcleo do caso de uso — sem essa capacidade, uma venda registrada não pode ser revisitada, conferida ou exibida novamente. É o complemento indispensável do registro de vendas (feature 002).

**Independent Test**: Pode ser testada isoladamente registrando uma venda, consultando-a pelo identificador retornado e verificando que todos os dados da resposta (cliente, filial, itens, descontos, totais) batem exatamente com o que foi persistido no registro.

**Acceptance Scenarios**:

1. **Given** uma venda registrada com sucesso, **When** o solicitante consulta essa venda pelo identificador retornado, **Then** o sistema responde com status 200 e a representação completa da venda, incluindo número da venda, data, cliente, filial, total geral e todos os itens com seus respectivos desconto e total.
2. **Given** uma venda registrada com múltiplos itens, **When** a venda é consultada, **Then** cada item retornado traz o produto (identificador e nome denormalizado), quantidade, preço unitário, percentual de desconto, valor de desconto e total do item.
3. **Given** uma venda consultada, **When** o solicitante inspeciona a resposta, **Then** não é necessária nenhuma chamada adicional a outro serviço para obter os nomes de cliente, filial ou produtos — todos já vêm denormalizados na resposta.

---

### User Story 2 - Consultar uma venda cancelada, total ou parcialmente (Priority: P2)

Um sistema cliente consulta uma venda que foi cancelada (integralmente ou apenas em um de seus itens) e recebe a venda normalmente, podendo identificar pelo campo de cancelamento — tanto no nível da venda quanto no nível de cada item — o que está ativo e o que foi cancelado, preservando o histórico completo.

**Why this priority**: Cancelamento é um estado do domínio (lógico, não uma remoção física) — sem essa capacidade, o valor de negócio de manter o histórico consultável ficaria incompleto, mesmo que o fluxo de registro (US1) já funcione. Depende do fluxo básico de consulta já existir.

**Independent Test**: Pode ser testada isoladamente registrando uma venda, cancelando-a (ou cancelando um de seus itens) e em seguida consultando-a, verificando que o recurso continua acessível com o status de cancelamento correto em cada nível (venda e item).

**Acceptance Scenarios**:

1. **Given** uma venda cancelada integralmente, **When** o solicitante a consulta pelo identificador, **Then** o sistema responde com status 200, o campo de cancelamento da venda indica que ela está cancelada, e o total geral reflete o valor zerado.
2. **Given** uma venda ativa com um item cancelado e outro ativo, **When** a venda é consultada, **Then** ambos os itens aparecem na resposta, cada um com seu próprio indicador de cancelamento, e o total geral da venda reflete apenas os itens ainda ativos.
3. **Given** uma venda cancelada, **When** ela é consultada repetidas vezes, **Then** a resposta permanece consistente e acessível indefinidamente — a consulta nunca deixa de encontrar uma venda apenas por ela estar cancelada.

---

### User Story 3 - Identificar claramente uma consulta a venda inexistente (Priority: P3)

Um sistema cliente tenta consultar uma venda usando um identificador que não corresponde a nenhum registro existente, e recebe uma resposta clara indicando que o recurso não foi encontrado, sem ambiguidade com outros tipos de erro.

**Why this priority**: Trata-se de um caso de borda importante para a robustez da integração, mas não bloqueia o valor central de consultar vendas existentes (US1) nem a visibilidade de vendas canceladas (US2). É a menor prioridade por afetar apenas o caminho de exceção.

**Independent Test**: Pode ser testada isoladamente enviando um identificador aleatório ou inexistente e confirmando que a resposta é de recurso não encontrado, sem expor detalhes internos do sistema.

**Acceptance Scenarios**:

1. **Given** um identificador que não corresponde a nenhuma venda registrada, **When** a consulta é realizada, **Then** o sistema responde com status 404 e uma mensagem indicando que a venda não foi encontrada.
2. **Given** uma resposta de venda não encontrada, **When** o solicitante inspeciona o corpo da resposta, **Then** o formato do erro segue o mesmo contrato usado pelos demais casos de uso da API.

---

### Edge Cases

- O que acontece quando o identificador informado não está em um formato válido de identificador técnico?
- Como o sistema se comporta ao consultar uma venda cujo último item ativo foi cancelado (fazendo a venda inteira ser cancelada por consequência)?
- O que acontece quando a venda possui um único item e ele está cancelado — a venda aparece com o total geral zerado?
- Consultas repetidas e simultâneas à mesma venda retornam sempre o mesmo resultado, sem side effects?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir consultar uma venda específica a partir do seu identificador técnico.
- **FR-002**: O sistema MUST retornar, para uma venda encontrada, a representação completa do registro: identificador, número da venda, data, cliente, filial, total geral, situação de cancelamento e a coleção completa de itens.
- **FR-003**: O sistema MUST incluir, para cada item da venda, o produto referenciado, a quantidade, o preço unitário, o percentual de desconto, o valor de desconto, o total do item e sua situação de cancelamento.
- **FR-004**: O sistema MUST retornar os dados de cliente, filial e produto já denormalizados (identificador e nome) na resposta, sem exigir consulta a nenhum outro serviço.
- **FR-005**: O sistema MUST retornar uma venda cancelada normalmente, com sua situação de cancelamento indicada como verdadeira, em vez de tratá-la como inexistente.
- **FR-006**: O sistema MUST incluir na resposta todos os itens da venda, ativos e cancelados, cada um com sua própria situação de cancelamento.
- **FR-007**: O sistema MUST responder com indicação de recurso não encontrado quando o identificador informado não corresponder a nenhuma venda existente.
- **FR-008**: O sistema MUST informar, na resposta de recurso não encontrado, uma mensagem clara e no mesmo formato de erro utilizado pelos demais casos de uso da API.
- **FR-009**: O sistema MUST tratar a consulta como uma operação somente leitura, sem alterar qualquer estado da venda consultada.
- **FR-010**: O sistema MUST NOT reprocessar ou revalidar regras de negócio de registro (cálculo de desconto, limites de quantidade, etc.) durante a consulta — apenas retornar os valores já calculados e persistidos.
- **FR-011**: O sistema MUST NOT disparar nenhum evento de domínio como resultado de uma consulta.

### Key Entities

- **Venda**: representa o registro de uma transação comercial já existente, consultada em sua íntegra — incluindo número de negócio, total geral, situação de cancelamento e a coleção de itens.
- **Item de venda**: representa uma linha da venda referente a um produto específico, retornada com quantidade, preço unitário, desconto, total e situação de cancelamento já calculados e persistidos no momento do registro ou de uma alteração anterior.
- **Cliente, Filial e Produto**: entidades pertencentes a outros domínios, retornadas na consulta apenas por identificador e nome denormalizados — não são buscadas, validadas nem alteradas por esta feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das consultas a vendas existentes retornam a representação completa do registro, incluindo todos os itens e seus valores calculados, sem exigir nenhuma chamada adicional a outro serviço.
- **SC-002**: 100% das consultas a vendas canceladas (total ou parcialmente) retornam o registro normalmente, com a situação de cancelamento correta em cada nível (venda e item).
- **SC-003**: 100% das consultas a identificadores inexistentes recebem uma resposta de recurso não encontrado, identificável sem ambiguidade em relação a outros tipos de erro.
- **SC-004**: 0% das consultas alteram qualquer dado da venda ou disparam eventos de domínio.
- **SC-005**: O solicitante consegue montar uma tela ou comprovante de venda completo a partir de uma única resposta de consulta, sem depender de chamadas a outros domínios.

## Assumptions

- O identificador utilizado na consulta é o identificador técnico da venda, o mesmo retornado pelo registro (feature 002) e usado nas demais operações sobre a venda.
- Não há paginação nem filtros nesta consulta — ela retorna sempre uma única venda completa a partir de um identificador exato; listagem com filtros e paginação pertence a uma feature futura.
- Autenticação e autorização do solicitante estão fora do escopo desta feature, assim como nas demais features da API.
- A consulta reflete o estado da venda no momento da requisição; não há suporte a consulta de versões históricas ou auditoria de alterações nesta feature.
- Alteração e cancelamento de vendas e de itens são especificados em features futuras e não fazem parte desta consulta.
