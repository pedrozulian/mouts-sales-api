# Feature Specification: Cancelar Item da Venda

**Feature Branch**: `007-cancelar-item-da-venda`

**Created**: 2026-08-11

**Status**: Draft

**Input**: User description: "Feature 007: Cancelar item da venda (UC-06 da documentação DDD no Notion). Como cliente da API, quero cancelar um item específico de uma venda (DELETE /api/sales/{id}/items/{itemId}) para que esse item seja marcado como cancelado e deixe de compor o total da venda, sem afetar os demais itens e sem que o registro seja fisicamente removido — o item cancelado permanece visível na consulta da venda. Ao carregar a venda pelo identificador informado na rota, se ela não existir o sistema responde 404. Se a venda já estiver cancelada, o cancelamento do item é rejeitado com 400 (venda cancelada é estado terminal e imutável — não aceita cancelamento de item). Em seguida o sistema localiza o item pelo identificador informado na rota; se o item não existir ou não pertencer à venda informada, responde 404. Se o item já estiver cancelado, a operação é rejeitada com 400, sem gerar evento duplicado. Ao cancelar um item válido, o sistema marca apenas esse item como cancelado e recalcula o total da venda somando unicamente os itens ainda ativos. Se, após esse cancelamento, não restar nenhum item ativo na venda, o sistema também cancela a venda inteira automaticamente — mesma consequência lógica de uma venda sem itens ativos não poder permanecer ativa. A resposta de sucesso é 204 sem corpo. O sistema emite um evento de cancelamento de item após a persistência bem-sucedida e, apenas quando o cancelamento do item esgotar os itens ativos da venda, emite também um evento de cancelamento da venda — na mesma operação, sem exigir uma segunda chamada. Assim como no cancelamento de venda (feature 006), duas solicitações de cancelamento quase simultâneas para o mesmo item devem ser tratadas de forma consistente: apenas uma é aplicada com sucesso, a outra é rejeitada com 400 como se o item já estivesse cancelado, nunca resultando em cancelamento duplicado ou eventos duplicados. Cancelamento da venda inteira via DELETE /api/sales/{id} é um caso de uso separado (feature 006) e não faz parte desta feature. Autenticação e autorização estão fora do escopo, como nas demais features da API."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cancelar um item ativo de uma venda ativa (Priority: P1)

Um sistema cliente (front-end, PDV ou BFF) solicita o cancelamento de um item específico de uma venda ativa, pelo identificador da venda e do item. O sistema marca apenas esse item como cancelado, recalcula o total da venda somando unicamente os itens ainda ativos, e mantém os demais itens inalterados.

**Why this priority**: É o núcleo do caso de uso — sem essa capacidade, um erro de lançamento em um único item (produto errado, quantidade errada) obriga a cancelar a venda inteira e recriá-la, mesmo quando os demais itens estão corretos. Complementa o cancelamento total da venda (feature 006) oferecendo uma granularidade que aquele não cobre.

**Independent Test**: Pode ser testada isoladamente registrando uma venda com múltiplos itens ativos, cancelando um deles, e verificando que apenas esse item passa a constar como cancelado, que o total da venda passa a refletir a soma dos itens restantes, e que os demais itens permanecem com seu estado original.

**Acceptance Scenarios**:

1. **Given** uma venda ativa com dois ou mais itens ativos, **When** o cancelamento de um desses itens é solicitado, **Then** o sistema marca apenas esse item como cancelado, recalcula o total da venda somando os itens ainda ativos e responde 204 sem corpo.
2. **Given** um item cancelado com sucesso, **When** a venda é consultada em seguida, **Then** o item continua visível na resposta, marcado como cancelado, e o total geral da venda reflete a soma apenas dos itens ainda ativos.
3. **Given** uma venda com três itens ativos, **When** um deles é cancelado, **Then** os outros dois permanecem ativos, com seus valores e descontos inalterados.

---

### User Story 2 - Cancelar o último item ativo encerra a venda (Priority: P2)

Ao cancelar um item que é o único ainda ativo na venda, o sistema reconhece que a venda não pode permanecer ativa sem nenhum item cobrável e cancela a venda inteira automaticamente, na mesma operação.

**Why this priority**: É uma consequência direta e obrigatória da regra de que toda venda ativa tem pelo menos um item ativo. Sem esse comportamento, o sistema permitiria o estado inconsistente de uma venda ativa sem nenhum item ativo. Depende da User Story 1 já existir, por isso é a segunda prioridade.

**Independent Test**: Pode ser testada isoladamente registrando uma venda com um único item (ou cancelando individualmente todos os itens de uma venda exceto o último), cancelando esse último item ativo, e verificando que tanto o item quanto a venda passam a constar como cancelados após uma única requisição.

**Acceptance Scenarios**:

1. **Given** uma venda ativa com exatamente um item ativo, **When** esse item é cancelado, **Then** o sistema marca o item como cancelado, marca a venda inteira como cancelada, zera o total da venda e responde 204 sem corpo.
2. **Given** uma venda com vários itens dos quais apenas um ainda está ativo, **When** esse último item ativo é cancelado, **Then** o sistema cancela o item e a venda na mesma operação, sem exigir uma segunda requisição.
3. **Given** uma venda cancelada porque seu último item ativo foi cancelado, **When** a venda é consultada em seguida, **Then** ela aparece marcada como cancelada, com todos os itens marcados como cancelados e total geral igual a zero.

---

### User Story 3 - Impedir cancelamento de item inválido (Priority: P2)

O sistema recusa a solicitação de cancelamento de item quando a venda não existe, quando a venda já está cancelada, quando o item não existe ou não pertence à venda informada, ou quando o item já está cancelado — informando claramente qual condição impediu a operação e sem alterar qualquer estado.

**Why this priority**: Sem essa proteção, itens já cancelados poderiam gerar eventos duplicados, e vendas em estado terminal poderiam ser reabertas indiretamente através de seus itens, comprometendo a confiabilidade do histórico que a User Story 1 depende. Tem a mesma prioridade da User Story 2 por serem, juntas, o conjunto de regras que protege a integridade do cancelamento.

**Independent Test**: Pode ser testada isoladamente solicitando o cancelamento de item para uma venda inexistente, para uma venda já cancelada, para um item inexistente, para um item de outra venda e para um item já cancelado — confirmando que nenhuma dessas solicitações altera qualquer estado e que a resposta identifica a condição violada.

**Acceptance Scenarios**:

1. **Given** um identificador de venda que não corresponde a nenhum registro existente, **When** o cancelamento de um item é solicitado, **Then** o sistema responde 404 sem alterar qualquer dado.
2. **Given** uma venda já cancelada, **When** o cancelamento de um de seus itens é solicitado, **Then** o sistema rejeita com 400, informando que a venda está em estado imutável, sem alterar o estado do item nem da venda.
3. **Given** uma venda existente e ativa, **When** o cancelamento é solicitado para um identificador de item que não pertence a essa venda, **Then** o sistema responde 404 sem alterar qualquer dado.
4. **Given** um item já cancelado anteriormente, **When** um novo cancelamento é solicitado para esse mesmo item, **Then** o sistema rejeita com 400, informando que o item já está cancelado, sem emitir evento nem alterar o total da venda.
5. **Given** uma solicitação de cancelamento de item rejeitada por qualquer uma das condições acima, **When** a venda é consultada em seguida (quando existente), **Then** seu estado e o de todos os seus itens permanecem exatamente como estavam antes da tentativa.
6. **Given** duas solicitações de cancelamento para o mesmo item ativo enviadas quase simultaneamente, **When** ambas são processadas, **Then** exatamente uma é aplicada com sucesso (204) e a outra é rejeitada com 400, como se o item já estivesse cancelado.

---

### User Story 4 - Rastrear o cancelamento do item por evento de domínio (Priority: P3)

Sempre que um item é cancelado com sucesso, o sistema registra um evento de cancelamento de item — e, quando esse cancelamento também encerra a venda por não restar item ativo, registra adicionalmente um evento de cancelamento da venda, na mesma operação.

**Why this priority**: Agrega valor de observabilidade e consistência com os demais casos de uso da API, mas o cancelamento em si (User Stories 1 e 2) já entrega o valor principal sem essa capacidade.

**Independent Test**: Pode ser testada isoladamente cancelando um item que não é o último ativo e verificando que exatamente um evento de cancelamento de item é emitido; e cancelando o último item ativo de uma venda, verificando que um evento de cancelamento de item e um evento de cancelamento da venda são emitidos juntos, na mesma operação.

**Acceptance Scenarios**:

1. **Given** uma venda ativa com mais de um item ativo, **When** um desses itens é cancelado com sucesso, **Then** o sistema emite exatamente um evento de cancelamento de item, e nenhum evento de cancelamento da venda.
2. **Given** uma venda ativa cujo item cancelado é o último ativo, **When** o cancelamento é concluído com sucesso, **Then** o sistema emite um evento de cancelamento de item e um evento de cancelamento da venda, ambos na mesma operação.
3. **Given** uma tentativa de cancelamento de item rejeitada por violar uma regra de negócio, **When** a rejeição ocorre, **Then** nenhum evento é emitido.

---

### Edge Cases

- Como o sistema se comporta ao tentar cancelar um item de uma venda cujo identificador nunca existiu?
- Como o sistema se comporta ao tentar cancelar um item cujo identificador nunca existiu, mesmo com uma venda válida informada?
- O que acontece quando o identificador do item informado pertence a uma venda diferente da informada na rota?
- Como o sistema se comporta ao cancelar o único item ativo restante de uma venda que já possuía outros itens cancelados individualmente antes?
- O que acontece com o total da venda imediatamente após o cancelamento do último item ativo — reflete zero da mesma forma que no cancelamento direto da venda inteira (feature 006)?
- Como o sistema se comporta quando duas solicitações de cancelamento para o mesmo item chegam quase simultaneamente?
- Como o sistema se comporta quando um cancelamento de item é solicitado no exato momento em que a venda inteira está sendo cancelada por outra requisição (feature 006)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir cancelar um item ativo específico de uma venda ativa existente, a partir do identificador da venda e do identificador do item.
- **FR-002**: O sistema MUST marcar apenas o item indicado como cancelado, mantendo inalterado o estado dos demais itens da venda.
- **FR-003**: O sistema MUST recalcular o total geral da venda após o cancelamento de um item, somando apenas os itens ainda ativos.
- **FR-004**: O sistema MUST NOT remover fisicamente o item ou a venda — o cancelamento é sempre lógico, e o item cancelado permanece visível na consulta da venda.
- **FR-005**: O sistema MUST rejeitar qualquer tentativa de cancelar um item de uma venda já cancelada, informando que a venda está em estado imutável, sem alterar o estado do item ou da venda.
- **FR-006**: O sistema MUST responder com indicação de recurso não encontrado quando o identificador da venda não corresponder a nenhum registro existente.
- **FR-007**: O sistema MUST responder com indicação de recurso não encontrado quando o identificador do item não corresponder a nenhum item existente na venda informada.
- **FR-008**: O sistema MUST rejeitar qualquer tentativa de cancelar um item que já esteja cancelado, informando essa condição, sem alterar o total da venda nem emitir evento.
- **FR-009**: O sistema MUST cancelar a venda inteira, além do item, quando o cancelamento do item resultar em nenhum item ativo remanescente na venda.
- **FR-010**: O sistema MUST retornar uma resposta de sucesso sem corpo quando o cancelamento do item for concluído.
- **FR-011**: O sistema MUST registrar um evento de cancelamento de item por cancelamento de item bem-sucedido.
- **FR-012**: O sistema MUST registrar também um evento de cancelamento da venda, na mesma operação, quando o cancelamento do item esgotar os itens ativos da venda (FR-009).
- **FR-013**: O sistema MUST NOT persistir qualquer mudança nem emitir qualquer evento quando a solicitação de cancelamento de item for rejeitada.
- **FR-014**: O sistema MUST informar ao solicitante, de forma específica, qual condição impediu o cancelamento quando a solicitação for rejeitada.
- **FR-015**: O sistema MUST garantir que, entre duas solicitações de cancelamento concorrentes para o mesmo item, apenas uma seja aplicada com sucesso — a outra MUST ser rejeitada com 400, como se o item já estivesse cancelado, nunca resultando em cancelamento duplicado ou em eventos duplicados para o mesmo item.

### Key Entities

- **Venda**: registro existente ao qual o item cancelado pertence. Tem seu total geral recalculado a partir dos itens ainda ativos e, quando o item cancelado é o último ativo, também passa a ter seu próprio estado marcado como cancelado.
- **Item de venda**: linha específica da venda sendo cancelada nesta feature. Passa a ter seu estado marcado como cancelado, permanecendo visível no histórico da venda, mas fora do cálculo do total.
- **Cliente, Filial e Produto**: entidades pertencentes a outros domínios, referenciadas apenas por identificador e nome denormalizados — não são afetadas por esta feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos cancelamentos de item bem-sucedidos resultam em um item marcado como cancelado e em um total geral da venda igual à soma exata dos itens ainda ativos.
- **SC-002**: 100% dos cancelamentos do último item ativo de uma venda resultam também na venda marcada como cancelada, em uma única requisição, sem exigir uma chamada adicional de cancelamento da venda.
- **SC-003**: 100% das tentativas de cancelar um item de venda inexistente, venda já cancelada, item inexistente, item de outra venda ou item já cancelado são rejeitadas sem qualquer persistência de mudança.
- **SC-004**: 100% dos cancelamentos de item bem-sucedidos produzem exatamente um evento de cancelamento de item — mais um evento de cancelamento da venda apenas quando o item cancelado for o último ativo.
- **SC-005**: Um item cancelado permanece consultável a qualquer momento após o cancelamento, junto com os demais itens da venda, refletindo seu estado correto.

## Assumptions

- Os identificadores utilizados no cancelamento são os identificadores técnicos da venda e do item, os mesmos usados na consulta (feature 003), no registro (feature 002) e na alteração (feature 005).
- Autenticação e autorização do solicitante estão fora do escopo desta feature, assim como nas demais features da API.
- Cancelamento da venda inteira em uma única operação explícita (`DELETE /api/sales/{id}`) é a feature 006, já implementada, e não é alterada por esta feature.
- O comportamento de concorrência adotado é o mesmo estabelecido na feature 006: entre duas requisições concorrentes que disputam o mesmo cancelamento, a que perde a corrida é tratada como se o alvo já estivesse cancelado (400), nunca como sucesso duplicado.
- Uma requisição de cancelamento de item que concorre com uma requisição de cancelamento da venda inteira (feature 006) para a mesma venda segue a mesma regra geral de conflito: apenas uma operação é aplicada com sucesso, a outra encontra o estado já alterado (venda cancelada ou item cancelado) e é rejeitada com 400.
