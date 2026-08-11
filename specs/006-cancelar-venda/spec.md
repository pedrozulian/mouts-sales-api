# Feature Specification: Cancelar Venda

**Feature Branch**: `006-cancelar-venda`

**Created**: 2026-08-10

**Status**: Draft

**Input**: User description: "Feature 006: Cancelar venda (UC-05 da documentação DDD no Notion). Como cliente da API, quero cancelar uma venda inteira (DELETE /api/sales/{id}) para que ela e todos os seus itens ainda ativos sejam marcados como cancelados e o total da venda seja zerado, sem que o registro seja fisicamente removido — o histórico contábil precisa permanecer consultável. Ao carregar a venda pelo identificador informado na rota, se ela não existir o sistema responde 404. Se a venda já estiver cancelada, o cancelamento é rejeitado com 400 (venda cancelada é estado terminal e imutável — não aceita novo cancelamento, alteração nem cancelamento de item). O cancelamento marca a venda e todos os itens ainda ativos como cancelados e zera o total geral da venda. A resposta de sucesso é 204 sem corpo. O cancelamento em massa dos itens não gera um evento de cancelamento por item — o único fato de negócio relevante é que a venda inteira foi cancelada, então apenas um evento de cancelamento da venda é emitido, após a persistência bem-sucedida. Cancelamento de item individual é um caso de uso separado e não faz parte desta feature. Autenticação e autorização estão fora do escopo, como nas demais features da API."

## Clarifications

### Session 2026-08-10

- Q: Quando duas requisições de cancelamento chegam quase ao mesmo tempo para a mesma venda ainda ativa, o sistema deve garantir que apenas a primeira seja aplicada, tratando a segunda como um cancelamento de venda já cancelada, ou é aceitável que ambas terminem em sucesso de forma idempotente? → A: Tratar como conflito — a requisição que perder a corrida recebe 400, igual ao cancelamento de venda já cancelada.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cancelar uma venda ativa (Priority: P1)

Um sistema cliente (front-end, PDV ou BFF) solicita o cancelamento de uma venda ativa pelo seu identificador. O sistema marca a venda e todos os seus itens ainda ativos como cancelados e zera o total geral da venda, sem remover fisicamente o registro do histórico.

**Why this priority**: É o núcleo do caso de uso — sem essa capacidade, uma venda registrada por engano ou invalidada por algum motivo de negócio não pode ser desfeita, mesmo que de forma lógica. É o complemento indispensável do registro (feature 002), da consulta (feature 003) e da alteração (feature 005) de vendas.

**Independent Test**: Pode ser testada isoladamente registrando uma venda com múltiplos itens, solicitando seu cancelamento, e verificando que a venda e todos os seus itens passam a constar como cancelados e que o total geral da venda passa a ser zero.

**Acceptance Scenarios**:

1. **Given** uma venda ativa com um ou mais itens ativos, **When** o cancelamento da venda é solicitado, **Then** o sistema marca a venda como cancelada, marca todos os seus itens ainda ativos como cancelados, zera o total geral da venda e responde 204 sem corpo.
2. **Given** uma venda cancelada com sucesso, **When** ela é consultada em seguida, **Then** a representação retornada reflete o novo estado — venda e itens marcados como cancelados, total geral igual a zero — permanecendo acessível para consulta.
3. **Given** uma venda ativa que já possuía algum item cancelado individualmente antes deste cancelamento, **When** o cancelamento da venda é solicitado, **Then** o sistema mantém esse item como já estava e cancela apenas os itens que ainda estavam ativos.

---

### User Story 2 - Impedir cancelamento inválido (Priority: P2)

O sistema recusa a solicitação de cancelamento quando a venda alvo não existe ou já está cancelada, informando claramente qual condição impediu a operação e sem alterar o estado atual da venda.

**Why this priority**: Sem essa proteção, o estado terminal de uma venda já cancelada poderia ser reaberto e eventos de cancelamento duplicados poderiam ser emitidos, comprometendo a confiabilidade do histórico que a User Story 1 depende. É a segunda prioridade porque pressupõe que o fluxo de cancelamento (US1) já exista.

**Independent Test**: Pode ser testada isoladamente solicitando o cancelamento de um identificador inexistente e de uma venda já cancelada, confirmando que nenhuma das duas solicitações altera qualquer estado e que a resposta identifica a condição violada.

**Acceptance Scenarios**:

1. **Given** um identificador de venda que não corresponde a nenhum registro existente, **When** o cancelamento é solicitado, **Then** o sistema responde 404 sem alterar qualquer dado.
2. **Given** uma venda já cancelada, **When** um novo cancelamento é solicitado para ela, **Then** o sistema rejeita com 400, informando que a venda está em estado imutável, sem alterar seu estado nem zerar novamente o total.
3. **Given** uma solicitação de cancelamento rejeitada por qualquer uma das condições acima, **When** a venda é consultada em seguida (quando existente), **Then** seu estado permanece exatamente como estava antes da tentativa.
4. **Given** duas solicitações de cancelamento para a mesma venda ativa enviadas quase simultaneamente, **When** ambas são processadas, **Then** exatamente uma é aplicada com sucesso (204) e a outra é rejeitada com 400, como se a venda já estivesse cancelada.

---

### User Story 3 - Rastrear o cancelamento da venda por evento de domínio (Priority: P3)

Sempre que uma venda é cancelada com sucesso, o sistema registra um único evento de cancelamento da venda — permitindo auditoria e rastreabilidade sem exigir consulta adicional e sem gerar ruído de um evento por item afetado.

**Why this priority**: Agrega valor de observabilidade e consistência com os demais casos de uso da API, mas o cancelamento em si (US1) já entrega o valor principal sem essa capacidade.

**Independent Test**: Pode ser testada isoladamente cancelando uma venda com vários itens ativos e verificando que exatamente um evento de cancelamento da venda é emitido, nunca um evento por item afetado.

**Acceptance Scenarios**:

1. **Given** uma venda ativa com três itens ativos, **When** seu cancelamento é concluído com sucesso, **Then** o sistema emite exatamente um evento de cancelamento da venda, e nenhum evento adicional por item.
2. **Given** uma tentativa de cancelamento rejeitada por violar uma regra de negócio, **When** a rejeição ocorre, **Then** nenhum evento é emitido.

---

### Edge Cases

- Como o sistema se comporta ao tentar cancelar uma venda cujo identificador nunca existiu?
- O que acontece quando uma venda já possui itens cancelados individualmente e o cancelamento da venda inteira é solicitado em seguida?
- Como o sistema se comporta se todos os itens de uma venda já estiverem cancelados individualmente (venda já teria sido cancelada automaticamente) e um cancelamento explícito da venda for solicitado?
- O que acontece com o total geral da venda quando ela é cancelada — permanece no valor anterior ao cancelamento em algum registro histórico, ou é definitivamente sobrescrito por zero?
- Como o sistema se comporta quando duas solicitações de cancelamento para a mesma venda chegam quase simultaneamente?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir cancelar uma venda ativa existente a partir do seu identificador.
- **FR-002**: O sistema MUST marcar a venda como cancelada e todos os seus itens ainda ativos como cancelados ao processar um cancelamento bem-sucedido.
- **FR-003**: O sistema MUST zerar o total geral da venda ao cancelá-la, refletindo que nenhum item permanece ativo e cobrável.
- **FR-004**: O sistema MUST NOT remover fisicamente o registro da venda ou de seus itens — o cancelamento é sempre lógico, e o registro permanece consultável.
- **FR-005**: O sistema MUST rejeitar qualquer tentativa de cancelar uma venda já cancelada, informando que o registro está em estado imutável, sem alterar seu estado.
- **FR-006**: O sistema MUST responder com indicação de recurso não encontrado quando o identificador da venda não corresponder a nenhum registro existente.
- **FR-007**: O sistema MUST retornar uma resposta de sucesso sem corpo quando o cancelamento for concluído.
- **FR-008**: O sistema MUST registrar um único evento de cancelamento da venda por solicitação de cancelamento bem-sucedida, independentemente da quantidade de itens afetados.
- **FR-009**: O sistema MUST NOT emitir um evento de cancelamento por item ao cancelar a venda inteira — o fato de negócio relevante é o cancelamento da venda como um todo.
- **FR-010**: O sistema MUST NOT persistir qualquer mudança nem emitir qualquer evento quando a solicitação de cancelamento for rejeitada.
- **FR-011**: O sistema MUST informar ao solicitante, de forma específica, qual condição impediu o cancelamento quando a solicitação for rejeitada.
- **FR-012**: O sistema MUST manter inalterado o estado de qualquer item que já estivesse cancelado individualmente antes do cancelamento da venda.
- **FR-013**: O sistema MUST garantir que, entre duas solicitações de cancelamento concorrentes para a mesma venda, apenas uma seja aplicada com sucesso — a outra MUST ser rejeitada com 400, como se a venda já estivesse cancelada, nunca resultando em duplo cancelamento ou em dois eventos de cancelamento para a mesma venda.

### Key Entities

- **Venda**: registro existente sendo cancelado. Passa a ter seu estado marcado como cancelado e seu total geral zerado, permanecendo consultável com o histórico de itens preservado.
- **Item de venda**: linha da venda que é marcada como cancelada quando ainda ativa no momento do cancelamento da venda; itens já cancelados individualmente antes dessa operação permanecem inalterados.
- **Cliente, Filial e Produto**: entidades pertencentes a outros domínios, referenciadas apenas por identificador e nome denormalizados — não são afetadas por esta feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos cancelamentos bem-sucedidos resultam em uma venda cujo estado e o de todos os seus itens ainda ativos refletem cancelamento, com total geral igual a zero.
- **SC-002**: 100% das tentativas de cancelar uma venda inexistente ou já cancelada são rejeitadas sem qualquer persistência de mudança.
- **SC-003**: 100% dos cancelamentos bem-sucedidos produzem exatamente um evento de cancelamento da venda — nunca um evento por item afetado.
- **SC-004**: O solicitante consegue cancelar uma venda inteira, com todos os seus itens, em uma única requisição, sem depender de chamadas adicionais de cancelamento de item.
- **SC-005**: Uma venda cancelada permanece consultável a qualquer momento após o cancelamento, com seu histórico de itens preservado.

## Assumptions

- O identificador utilizado no cancelamento é o identificador técnico da venda, o mesmo usado na consulta (feature 003), no registro (feature 002) e na alteração (feature 005).
- Autenticação e autorização do solicitante estão fora do escopo desta feature, assim como nas demais features da API.
- Cancelamento de um item individual da venda é uma operação independente, especificada em uma feature futura, e não é afetada nem substituída por esta feature.
- Uma venda cujo último item ativo é cancelado individualmente (fora desta feature) segue regra própria de transição automática para o estado cancelado, não coberta por este cancelamento explícito da venda inteira.
