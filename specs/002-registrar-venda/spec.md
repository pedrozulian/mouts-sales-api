# Feature Specification: Registrar Venda

**Feature Branch**: `002-registrar-venda`

**Created**: 2026-08-09

**Status**: Draft

**Input**: User description: "Feature 002: Registrar venda (UC-01 da documentação DDD no Notion). Como cliente da API, quero registrar uma nova venda (POST /api/sales) informando cliente, filial e um ou mais itens de produto, para que o sistema calcule automaticamente o desconto por quantidade, os totais de cada item e o total da venda, e persista o registro."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar venda com desconto calculado automaticamente (Priority: P1)

Um sistema cliente (front-end, PDV ou BFF) registra uma nova venda informando o cliente, a filial e um ou mais itens de produto com suas quantidades e preços unitários. O sistema calcula automaticamente o desconto de cada item conforme a quantidade, os totais por item e o total geral da venda, e devolve a venda completa já registrada.

**Why this priority**: É o núcleo do caso de uso — sem essa capacidade não existe registro de vendas. Toda a proposta de valor da feature (cálculo automático e correto de desconto) está aqui.

**Independent Test**: Pode ser testada isoladamente enviando uma venda válida com um item cuja quantidade caia em cada uma das faixas de desconto (ex.: 2, 5 e 15 unidades) e verificando que o desconto, o total do item e o total da venda retornados batem com o esperado.

**Acceptance Scenarios**:

1. **Given** um cliente, uma filial e um item com 2 unidades de um produto, **When** a venda é registrada, **Then** o sistema aceita o registro, aplica 0% de desconto ao item e retorna a venda com um número de venda gerado.
2. **Given** um item com 5 unidades do mesmo produto, **When** a venda é registrada, **Then** o sistema aplica 10% de desconto sobre o valor bruto do item.
3. **Given** um item com 15 unidades do mesmo produto, **When** a venda é registrada, **Then** o sistema aplica 20% de desconto sobre o valor bruto do item.
4. **Given** uma venda com dois itens de produtos diferentes, **When** a venda é registrada, **Then** o total da venda é a soma dos totais (já com desconto) de cada item, calculados de forma independente.
5. **Given** uma venda registrada com sucesso, **When** o solicitante consulta a resposta, **Then** encontra o número de venda gerado pelo sistema, a data da venda, e o desconto e o total calculados de cada item.

---

### User Story 2 - Impedir vendas que violem as regras de quantidade e de negócio (Priority: P2)

O sistema recusa o registro de uma venda quando qualquer item ultrapassa o limite de unidades permitido, quando o mesmo produto aparece mais de uma vez na venda, quando não há nenhum item, ou quando um preço unitário é inválido — informando claramente qual regra foi violada.

**Why this priority**: Sem essa proteção, a regra de negócio central (limites de desconto e de quantidade) pode ser contornada, e o valor da User Story 1 fica comprometido. É a segunda prioridade porque depende do fluxo de registro já existir.

**Independent Test**: Pode ser testada isoladamente enviando payloads inválidos (quantidade 21, dois itens do mesmo produto, venda sem itens, preço zero) e confirmando que nenhum é registrado e que a resposta identifica a regra violada.

**Acceptance Scenarios**:

1. **Given** um item com 21 unidades do mesmo produto, **When** a venda é registrada, **Then** o sistema rejeita o registro e informa que o limite de 20 unidades foi excedido.
2. **Given** uma venda com dois itens referenciando o mesmo produto, **When** a venda é registrada, **Then** o sistema rejeita o registro e informa que o produto está duplicado.
3. **Given** uma venda sem nenhum item, **When** o registro é solicitado, **Then** o sistema rejeita e informa que ao menos um item é obrigatório.
4. **Given** um item com preço unitário zero ou negativo, **When** a venda é registrada, **Then** o sistema rejeita o registro e informa que o preço é inválido.
5. **Given** uma venda rejeitada por qualquer uma das regras acima, **When** o solicitante consulta o registro posteriormente, **Then** nenhuma venda correspondente foi persistida.

---

### User Story 3 - Rastrear a criação da venda por evento de domínio (Priority: P3)

Sempre que uma venda é registrada com sucesso, o sistema registra um evento de criação com os dados essenciais da venda, permitindo auditoria e rastreabilidade sem exigir consulta adicional à API.

**Why this priority**: Agrega valor de observabilidade e é um diferencial citado no caso de uso original, mas o registro da venda em si (US1) já entrega o valor principal sem essa capacidade.

**Independent Test**: Pode ser testada isoladamente registrando uma venda válida e verificando que um evento de criação correspondente foi emitido com o identificador e o número da venda.

**Acceptance Scenarios**:

1. **Given** uma venda registrada com sucesso, **When** o registro é concluído, **Then** o sistema emite um evento de criação contendo o identificador da venda, o número da venda, o cliente, a filial e o total.
2. **Given** uma tentativa de registro rejeitada por violar uma regra de negócio, **When** o registro falha, **Then** nenhum evento de criação é emitido.

---

### Edge Cases

- O que acontece quando a quantidade de um item está exatamente na fronteira de uma faixa de desconto (3, 4, 9, 10 ou 20 unidades)?
- Como o sistema se comporta quando a data da venda não é informada no pedido?
- O que acontece quando o identificador ou o nome do cliente, da filial ou de um produto vêm vazios ou ausentes?
- Como o sistema garante que dois registros simultâneos não recebam o mesmo número de venda?
- O que acontece quando a venda tem vários itens e apenas um deles viola uma regra de negócio — a venda inteira é rejeitada ou só o item?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST permitir o registro de uma nova venda contendo cliente, filial e um ou mais itens de produto, com data da venda opcional.
- **FR-002**: O sistema MUST calcular automaticamente o desconto de cada item com base na quantidade de unidades do mesmo produto no item, segundo as faixas: 0% para 1 a 3 unidades, 10% para 4 a 9 unidades, 20% para 10 a 20 unidades.
- **FR-003**: O sistema MUST rejeitar o registro de qualquer item com quantidade superior a 20 unidades do mesmo produto.
- **FR-004**: O sistema MUST rejeitar o registro de uma venda sem nenhum item informado.
- **FR-005**: O sistema MUST rejeitar o registro de uma venda em que o mesmo produto apareça em mais de um item.
- **FR-006**: O sistema MUST rejeitar o registro de qualquer item com preço unitário menor ou igual a zero.
- **FR-007**: O sistema MUST rejeitar o registro quando o identificador ou o nome do cliente, da filial, ou de qualquer produto informado estiver ausente ou vazio.
- **FR-008**: O sistema MUST calcular o valor total de cada item (valor bruto menos o desconto aplicável) e o valor total da venda como a soma dos totais dos itens.
- **FR-009**: O sistema MUST gerar automaticamente um número de venda único e legível para cada venda registrada, sem aceitar esse valor do solicitante.
- **FR-010**: O sistema MUST ignorar qualquer valor de desconto ou de total enviado na requisição, calculando-os sempre internamente.
- **FR-011**: O sistema MUST assumir o momento do registro como data da venda quando esta não for informada.
- **FR-012**: O sistema MUST retornar, após o registro bem-sucedido, a representação completa da venda, incluindo o número gerado, a data, o desconto e o total calculados de cada item, e o total geral.
- **FR-013**: O sistema MUST informar ao solicitante, de forma específica, qual regra de negócio foi violada quando o registro for rejeitado.
- **FR-014**: O sistema MUST rejeitar o registro por completo — sem persistir nenhum item — quando qualquer item da venda violar uma regra de negócio.
- **FR-015**: O sistema MUST registrar um evento de criação da venda, contendo seus dados essenciais, sempre que o registro for concluído com sucesso.

### Key Entities

- **Venda**: representa o registro de uma transação comercial realizada por um cliente em uma filial em uma data específica. Possui um número de negócio único, um total geral e uma coleção de itens.
- **Item de venda**: representa uma linha da venda referente a um produto específico. Possui quantidade, preço unitário, percentual de desconto, valor de desconto e total calculados a partir da quantidade e do preço informados.
- **Cliente, Filial e Produto**: entidades pertencentes a outros domínios, referenciadas na venda apenas por identificador e nome — não são criadas, alteradas nem validadas por esta feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das vendas registradas com um item em cada faixa de desconto (incluindo as fronteiras 3, 4, 9, 10 e 20 unidades) retornam o percentual e o valor de desconto corretos.
- **SC-002**: 100% das tentativas de registro que violam uma regra de negócio (quantidade acima de 20, produto duplicado, venda sem itens, preço inválido, identidade externa incompleta) são rejeitadas sem persistir qualquer dado da venda.
- **SC-003**: 100% das vendas registradas com sucesso recebem um número de venda único, sem colisão, mesmo sob registros concorrentes.
- **SC-004**: 100% das vendas registradas com sucesso produzem um evento de criação rastreável, com os dados essenciais da venda.
- **SC-005**: O solicitante consegue identificar, a partir da resposta de um registro rejeitado, exatamente qual regra de negócio foi violada, sem precisar de suporte adicional para interpretar o erro.

## Assumptions

- Quem consome a API (front-end, PDV ou BFF) já selecionou o cliente, a filial e os produtos antes da chamada, e portanto fornece o identificador e o nome de cada um — conforme o padrão de External Identities adotado pelo domínio de vendas.
- Não há validação da existência desses identificadores em nenhum outro sistema; essa responsabilidade pertence aos domínios de origem de cliente, filial e produto, fora da fronteira desta feature.
- Todos os valores monetários são tratados em uma única moeda, sem conversão de câmbio.
- O evento de criação da venda é registrado no log estruturado da aplicação; a publicação em um message broker real está fora do escopo desta feature.
- Autenticação e autorização do solicitante estão fora do escopo desta feature.
- Consulta, listagem, alteração e cancelamento de vendas e de itens serão especificados em features futuras, e não fazem parte deste registro.
