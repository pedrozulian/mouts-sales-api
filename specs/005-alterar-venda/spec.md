# Feature Specification: Alterar Venda

**Feature Branch**: `005-alterar-venda`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "Feature 005: Alterar venda (UC-04 da documentação DDD no Notion). Como cliente da API, quero atualizar uma venda existente (PUT /api/sales/{id}) enviando o estado completo desejado — cabeçalho (cliente, filial, data) e lista de itens — para que o sistema reconcilie os itens da venda com o que foi enviado, preservando o histórico de qualquer item removido em vez de apagá-lo fisicamente. O corpo da requisição tem o mesmo formato do registro de venda (UC-01), com um campo opcional itemId em cada item, e a reconciliação segue estritamente três regras: item com itemId conhecido é atualizado (quantidade e/ou preço unitário); item sem itemId é adicionado à venda como novo; item que já existia na venda mas não aparece no corpo da requisição é cancelado logicamente, nunca removido do banco — assim o PUT mantém semântica REST de substituição sem destruir o histórico contábil da venda. Ao carregar a venda pelo identificador informado na rota, se ela não existir o sistema responde 404. Se a venda já estiver cancelada, a alteração é rejeitada com 400 (venda cancelada é estado terminal e imutável — não aceita nem alteração de cabeçalho nem de itens). O corpo deve conter ao menos um item; corpo sem nenhum item é rejeitado com 400. Um itemId informado no corpo que não pertence à venda carregada também é rejeitado com 400. Cada item, seja atualizado ou adicionado, respeita as mesmas invariantes de negócio do registro: quantidade entre 1 e 20 unidades do mesmo produto, e o mesmo produto não pode aparecer duas vezes no corpo da requisição. Desconto e totais nunca são aceitos do cliente — são sempre recalculados pelo domínio a partir da política de desconto por faixa de quantidade, tanto para itens novos quanto para itens cuja quantidade mudou. O total geral da venda é recalculado somando apenas os itens que permanecerem ativos após a reconciliação. Ao final de uma alteração bem-sucedida, o sistema persiste tudo em uma única transação e, após o commit, despacha um evento de cancelamento para cada item removido implicitamente pela reconciliação, seguido de um único evento de alteração da venda como um todo. A resposta de sucesso é 200 com a representação completa e atualizada da venda, no mesmo formato usado pela consulta (UC-02), incluindo os itens cancelados na reconciliação já refletidos com seu isCancelled true."

## Clarifications

### Session 2026-08-09

- Q: Quando o pedido de alteração referencia um itemId existente mas com um product diferente do produto atualmente armazenado nesse item, como o sistema deve reagir? → A: Rejeitar o pedido com 400 — produto de um item existente é imutável; para trocar de produto é preciso cancelar o item e adicionar um novo.
- Q: Quando o pedido de alteração referencia, pelo itemId, um item que já estava cancelado antes desta alteração ser solicitada, como o sistema deve reagir? → A: Rejeitar o pedido com 400 — item cancelado não pode ser referenciado; identificador é tratado como inexistente na venda.
- Q: No pedido de alteração (PUT), o campo saleDate é obrigatório, ou pode ser omitido — mantendo a data atualmente registrada na venda? → A: saleDate obrigatório no corpo do PUT — ausência é rejeitada com 400.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Alterar cabeçalho e reconciliar itens de uma venda ativa (Priority: P1)

Um sistema cliente (front-end, PDV ou BFF) envia o estado completo desejado de uma venda ativa — cliente, filial, data e a lista de itens — e o sistema reconcilia automaticamente os itens: os já conhecidos são atualizados, os novos são adicionados, e os que deixaram de aparecer no pedido são cancelados logicamente, sem serem apagados. O resultado é a venda atualizada, com desconto e total recalculados.

**Why this priority**: É o núcleo do caso de uso — sem essa capacidade, uma venda registrada não pode ser corrigida ou ajustada após a criação. É o complemento indispensável do registro (feature 002) e da consulta (feature 003) de vendas.

**Independent Test**: Pode ser testada isoladamente registrando uma venda com múltiplos itens, enviando um pedido de alteração que atualiza a quantidade de um item, adiciona um item novo e omite um item existente, e verificando que a venda resultante reflete exatamente essas três reconciliações.

**Acceptance Scenarios**:

1. **Given** uma venda ativa com um item existente, **When** o pedido de alteração referencia esse item pelo seu identificador com uma nova quantidade, **Then** o sistema atualiza a quantidade, recalcula o desconto e o total do item e o total geral da venda, e responde 200.
2. **Given** uma venda ativa, **When** o pedido de alteração inclui um item sem identificador, **Then** o sistema adiciona esse item à venda como novo, calcula seu desconto e total, e o inclui no total geral.
3. **Given** uma venda ativa com três itens, **When** o pedido de alteração envia o corpo contendo apenas dois desses itens, **Then** o item ausente é marcado como cancelado — não removido do registro —, deixa de compor o total geral, mas continua visível na venda.
4. **Given** uma venda ativa, **When** o pedido de alteração informa um novo cliente, filial e/ou data, **Then** esses dados do cabeçalho são atualizados sem afetar os itens que não foram alterados.
5. **Given** uma venda alterada com sucesso, **When** ela é consultada em seguida, **Then** a representação retornada reflete exatamente o novo estado persistido — cabeçalho e itens reconciliados.

---

### User Story 2 - Impedir alterações que violem invariantes de negócio (Priority: P2)

O sistema recusa um pedido de alteração quando a venda alvo já está cancelada, quando o corpo não contém nenhum item, quando referencia um item que não pertence à venda, ou quando algum item viola as regras de quantidade e duplicidade de produto — informando claramente qual condição impediu a alteração.

**Why this priority**: Sem essa proteção, o estado terminal de uma venda cancelada poderia ser reaberto e as mesmas regras de quantidade e duplicidade do registro poderiam ser contornadas na alteração, comprometendo a integridade que a User Story 1 depende. É a segunda prioridade porque pressupõe que o fluxo de alteração (US1) já exista.

**Independent Test**: Pode ser testada isoladamente enviando pedidos de alteração inválidos (venda já cancelada, corpo sem itens, itemId inexistente na venda, quantidade fora do intervalo, produto duplicado) e confirmando que nenhum deles é aplicado e que a resposta identifica a condição violada.

**Acceptance Scenarios**:

1. **Given** uma venda já cancelada, **When** um pedido de alteração é enviado para ela, **Then** o sistema rejeita com 400 e informa que a venda está em estado imutável.
2. **Given** um pedido de alteração com corpo sem nenhum item, **When** ele é enviado, **Then** o sistema rejeita com 400.
3. **Given** um pedido de alteração que referencia um identificador de item que não pertence à venda carregada — incluindo o identificador de um item já cancelado nessa venda —, **When** ele é enviado, **Then** o sistema rejeita com 400.
4. **Given** um item, novo ou atualizado, com quantidade fora do intervalo de 1 a 20 unidades, **When** o pedido de alteração é enviado, **Then** o sistema rejeita com 400.
5. **Given** um pedido de alteração com dois itens referenciando o mesmo produto, **When** ele é enviado, **Then** o sistema rejeita com 400.
6. **Given** um identificador de venda que não corresponde a nenhum registro existente, **When** um pedido de alteração é enviado para ele, **Then** o sistema responde 404.
7. **Given** um item existente referenciado pelo seu identificador, **When** o pedido de alteração informa para ele um produto diferente do produto atualmente registrado nesse item, **Then** o sistema rejeita com 400, sem alterar o produto do item.
8. **Given** um pedido de alteração sem o campo de data no cabeçalho, **When** ele é enviado, **Then** o sistema rejeita com 400.
9. **Given** um pedido de alteração rejeitado por qualquer uma das condições acima, **When** a venda é consultada em seguida, **Then** seu estado permanece exatamente como estava antes da tentativa.

---

### User Story 3 - Rastrear a alteração da venda por eventos de domínio (Priority: P3)

Sempre que uma venda é alterada com sucesso, o sistema registra um evento de alteração da venda e, para cada item removido implicitamente pela reconciliação, um evento de cancelamento — permitindo auditoria e rastreabilidade sem exigir consulta adicional.

**Why this priority**: Agrega valor de observabilidade e consistência com os demais casos de uso da API, mas a alteração em si (US1) já entrega o valor principal sem essa capacidade.

**Independent Test**: Pode ser testada isoladamente executando uma alteração que remove implicitamente um item e outra que apenas atualiza itens existentes, verificando que a primeira emite o evento de cancelamento do item mais o evento de alteração da venda, e a segunda emite somente o evento de alteração da venda.

**Acceptance Scenarios**:

1. **Given** uma alteração bem-sucedida que remove implicitamente dois itens da venda, **When** a operação é concluída, **Then** o sistema emite um evento de cancelamento para cada um dos dois itens, seguido de um único evento de alteração da venda.
2. **Given** uma alteração bem-sucedida que apenas atualiza e adiciona itens, sem remover nenhum, **When** a operação é concluída, **Then** o sistema emite apenas o evento de alteração da venda, sem nenhum evento de cancelamento de item.
3. **Given** uma tentativa de alteração rejeitada por violar uma regra de negócio, **When** a rejeição ocorre, **Then** nenhum evento é emitido.

---

### Edge Cases

- O que acontece quando a alteração de quantidade de um item o move de uma faixa de desconto para outra (por exemplo, de 3 para 4 unidades)?
- Como o sistema se comporta quando o pedido de alteração não modifica nenhum item — apenas cliente, filial ou data no cabeçalho?
- O que acontece quando o mesmo identificador de item aparece duas vezes no corpo do pedido de alteração?
- Como o sistema se comporta ao alterar simultaneamente o preço unitário e a quantidade de um mesmo item existente?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir alterar uma venda existente e ativa, substituindo o cabeçalho (cliente, filial, data) e reconciliando os itens conforme o estado descrito no pedido de alteração.
- **FR-002**: O sistema MUST exigir cliente, filial e data no pedido de alteração — diferente do registro, em que a data é opcional — rejeitando com 400 o pedido em que qualquer um desses campos do cabeçalho esteja ausente.
- **FR-003**: O sistema MUST atualizar um item existente da venda quando o pedido de alteração referenciar seu identificador, recalculando desconto e total conforme a nova quantidade e/ou preço unitário.
- **FR-004**: O sistema MUST rejeitar o pedido de alteração quando o item referenciado por um identificador existente informar um produto diferente do produto atualmente registrado nesse item — o produto de um item existente é imutável; trocar de produto exige cancelar o item e adicionar um novo.
- **FR-005**: O sistema MUST adicionar um novo item à venda quando o pedido de alteração incluir um item sem identificador, calculando seu desconto e total da mesma forma que no registro original.
- **FR-006**: O sistema MUST cancelar logicamente, sem remover do histórico, todo item que pertencia à venda e não aparecer no pedido de alteração.
- **FR-007**: O sistema MUST recalcular o total geral da venda somando apenas os itens que permanecerem ativos após a reconciliação.
- **FR-008**: O sistema MUST rejeitar qualquer tentativa de alteração de uma venda já cancelada, informando que o registro está em estado imutável.
- **FR-009**: O sistema MUST rejeitar o pedido de alteração que não contenha nenhum item.
- **FR-010**: O sistema MUST rejeitar o pedido de alteração que referencie um identificador de item que não pertence à venda carregada, incluindo o identificador de um item já cancelado nessa venda — item cancelado não pode ser reativado por esta operação.
- **FR-011**: O sistema MUST rejeitar qualquer item, novo ou atualizado, com quantidade fora do intervalo de 1 a 20 unidades do mesmo produto.
- **FR-012**: O sistema MUST rejeitar o pedido de alteração em que o mesmo produto apareça em mais de um item.
- **FR-013**: O sistema MUST responder com indicação de recurso não encontrado quando o identificador da venda não corresponder a nenhum registro existente.
- **FR-014**: O sistema MUST ignorar qualquer valor de desconto ou de total enviado no pedido de alteração, calculando-os sempre internamente.
- **FR-015**: O sistema MUST retornar, após a alteração bem-sucedida, a representação completa e atualizada da venda, incluindo os itens cancelados pela reconciliação com sua situação de cancelamento refletida.
- **FR-016**: O sistema MUST registrar um evento de cancelamento para cada item removido implicitamente pela reconciliação.
- **FR-017**: O sistema MUST registrar um único evento de alteração da venda por pedido de alteração bem-sucedido, independentemente da quantidade de itens afetados.
- **FR-018**: O sistema MUST NOT persistir qualquer mudança nem emitir qualquer evento quando o pedido de alteração for rejeitado por violar uma regra de negócio.
- **FR-019**: O sistema MUST informar ao solicitante, de forma específica, qual condição ou regra de negócio impediu a alteração quando o pedido for rejeitado.

### Key Entities

- **Venda**: registro existente sendo alterado. Seu cabeçalho (cliente, filial, data) é substituído pelo pedido de alteração, seus itens são reconciliados conforme as regras de atualização, adição e cancelamento implícito, e seu total geral é recalculado a partir dos itens que permanecerem ativos.
- **Item de venda**: linha da venda que pode ser atualizada (nova quantidade e/ou preço unitário), adicionada (novo produto sem identificador prévio) ou cancelada implicitamente (ausente do pedido de alteração) durante a reconciliação.
- **Cliente, Filial e Produto**: entidades pertencentes a outros domínios, referenciadas na alteração apenas por identificador e nome denormalizados — não são criadas, buscadas nem validadas quanto à sua existência por esta feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos pedidos de alteração válidos resultam em uma venda cujo estado (cabeçalho e itens) reflete exatamente o que foi enviado, incluindo os itens implicitamente cancelados por terem sido omitidos.
- **SC-002**: 100% das tentativas de alterar uma venda já cancelada são rejeitadas sem qualquer persistência de mudança.
- **SC-003**: 100% dos pedidos de alteração que violam uma regra de negócio (quantidade fora do intervalo, produto duplicado, item inexistente na venda, corpo sem itens) são rejeitados com identificação clara da condição violada.
- **SC-004**: 100% das alterações bem-sucedidas produzem exatamente um evento de alteração da venda, mais um evento de cancelamento para cada item removido implicitamente — nunca mais, nunca menos.
- **SC-005**: O solicitante consegue reconciliar o estado completo dos itens de uma venda — atualizar, adicionar e remover — em uma única requisição, sem depender de chamadas adicionais de cancelamento de item.

## Assumptions

- O identificador utilizado na alteração é o identificador técnico da venda, o mesmo usado na consulta (feature 003) e retornado no registro (feature 002).
- O pedido de alteração segue o mesmo formato de entrada do registro de venda (feature 002), acrescido de um identificador opcional por item para viabilizar a reconciliação.
- Autenticação e autorização do solicitante estão fora do escopo desta feature, assim como nas demais features da API.
- Cancelamento total da venda e cancelamento isolado de um item permanecem operações independentes desta alteração, especificadas em features futuras.
