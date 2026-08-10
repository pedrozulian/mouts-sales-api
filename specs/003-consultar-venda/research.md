# Research: Consultar Venda

Nenhum `NEEDS CLARIFICATION` restou no Technical Context do `plan.md` — a documentação DDD
(Notion) e o código já entregue pela feature 002 (`002-registrar-venda`) já fixam a maior
parte das escolhas. Este documento registra as decisões técnicas específicas desta feature.

## 1. Leitura do agregado `Sale`

**Decision**: a query usa `IApplicationDbContext.Sales` diretamente, com
`.AsNoTracking().Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)`.

**Rationale**: é uma operação somente leitura — não há necessidade de rastreamento de
mudanças pelo `ChangeTracker`, e `Items` é um relacionamento `HasMany` comum (não *owned*),
portanto exige `Include` explícito para não ser omitido silenciosamente. Consistente com o
padrão já documentado para UC-03 ("A query é somente-leitura e usa `AsNoTracking`").

**Alternatives considered**:
- **Lazy loading via proxies**: exigiria adicionar `Microsoft.EntityFrameworkCore.Proxies`
  (nova dependência fora da Stack Tecnológica Obrigatória) e tornaria implícito um custo de
  N+1 query; rejeitado.
- **Projeção direta para `SaleResponse` via `Select`**: evitaria carregar a entidade completa,
  mas duplicaria a forma do DTO na query LINQ, divergindo do mapeamento Mapster já registrado
  para `Sale → SaleResponse`; rejeitado em favor de reaproveitar o mapeamento existente.

## 2. Tradução de "venda não encontrada" para HTTP 404

**Decision**: `GetSaleQueryHandler` retorna `Result<SaleResponse>`, igual ao padrão já usado em
`CreateSaleCommandHandler`. Quando a venda não é encontrada, retorna
`Result<SaleResponse>.Failure(new Notification("id", "Venda não encontrada."))`. O endpoint
traduz qualquer falha desta query especificamente para `404 Not Found` (não `400`, já que não
se trata de uma regra de negócio violada, e sim de ausência do recurso) — mantendo o mesmo
formato de corpo (`{ "errors": [...] }`) usado pelos demais erros da API, conforme a tabela
"Contrato de erro" da documentação de Use Cases no Notion.

**Rationale**: mantém uniformidade com o Princípio VII (Result/Notification, nunca exception)
sem introduzir um tipo de retorno paralelo só para esta query.

**Alternatives considered**:
- **Handler retorna `SaleResponse?` (nulo quando não encontrado)**: mais simples, mas quebra o
  padrão uniforme de retorno usado pelos demais handlers da Application; rejeitado por
  consistência.
- **Lançar exception customizada (`NotFoundException`) capturada por middleware global**:
  proibido pelo Princípio VII, que reserva exceptions para falhas de infraestrutura ou
  invariantes de programação violadas — "não encontrado" é fluxo esperado, não excepcional.

## 3. Reaproveitamento de DTOs e mapeamento Mapster

**Decision**: nenhum novo DTO é criado. A query reutiliza `SaleResponse`, `SaleItemResponse` e
`ExternalReferenceResponse` (já existentes em `SalesApi.Application.Sales.Dtos`) e o
mapeamento `Sale → SaleResponse` / `SaleItem → SaleItemResponse`, já registrado globalmente em
`CreateSaleMappingConfig` (`TypeAdapterConfig` é compartilhado pela aplicação inteira, não é
escopado por caso de uso).

**Rationale**: evita duplicar a forma de resposta já validada pela feature 002 e reduz a
superfície de mudança desta feature a Application (Query + Handler) e Api (endpoint).

**Alternatives considered**: criar um `GetSaleMappingConfig` próprio — rejeitado por ser
redundante; o registro já existe e cobre exatamente os mesmos tipos de origem/destino.

## 4. Observabilidade da consulta

**Decision**: logging estruturado explícito dentro de `GetSaleQueryHandler` (`ILogger`
injetado por construtor): `LogInformation` quando a venda é encontrada, `LogWarning` quando
não é encontrada — incluindo o `id` consultado em ambos os casos.

**Rationale**: o Princípio VIII exige logging estruturado em "toda operação relevante
(comandos, queries, eventos de domínio, ...)". Diferente do UC-01, esta feature não dispara
nenhum evento de domínio (FR-011), então não há um `INotificationHandler` para carregar o log
— o próprio handler da query assume essa responsabilidade.

**Alternatives considered**: nenhum — é a única forma de logging estruturado aplicável a uma
Query sem evento de domínio.

## 5. Validação de formato do identificador na rota

**Decision**: a rota usa o route constraint nativo do ASP.NET Core Minimal API,
`/api/sales/{id:guid}`. Valores que não podem ser convertidos para `Guid` já resultam em `404`
automático do próprio roteamento (constraint não satisfeita), sem necessidade de validação
manual adicional na Application.

**Rationale**: evita código de validação redundante para um caso já coberto nativamente pelo
framework; mantém a Application focada exclusivamente na existência do registro.

**Alternatives considered**: aceitar `string` na rota e validar o parsing do `Guid` dentro do
handler, retornando `400` — mais explícito, mas adicionaria uma verificação que o próprio
framework já garante; rejeitado por complexidade desnecessária.

## 6. Testes para cenários de venda cancelada (User Story 2)

**Decision**: como o cancelamento de vendas e de itens (UC-05/UC-06) ainda não foi
implementado — o agregado `Sale` hoje só expõe `Create` — os testes desta feature que exercem
"venda cancelada" ou "item cancelado" preparam o estado diretamente no banco durante o Arrange
(ex.: `Sale.Create` seguido de uma atualização direta via SQL/EF Core seed ajustando
`is_cancelled` e `total_amount`), sem passar por um método de domínio que ainda não existe.

**Rationale**: a query de consulta (UC-02) deve validar que ela expõe corretamente um estado
já persistido, independentemente de qual feature futura (UC-05/UC-06) produzirá esse estado em
produção. Bloquear os testes desta feature até UC-05/UC-06 existirem atrasaria
desnecessariamente a entrega do UC-02, que é uma leitura pura e não depende do fluxo de
cancelamento estar implementado.

**Alternatives considered**:
- **Adiar User Story 2 até UC-05/UC-06 existirem**: rejeitado — a spec já define UC-02 como
  capaz de exibir vendas canceladas (FR-005/FR-006), e o valor de negócio (histórico sempre
  consultável) precisa estar coberto por teste desde já.
- **Antecipar um método `Cancel`/`CancelItem` no agregado só para viabilizar o teste**:
  rejeitado — anteciparia parte do desenho de UC-05/UC-06 sem que a spec correspondente exista
  ainda, criando acoplamento indevido entre features.

**Output**: todas as decisões técnicas necessárias para o desenho estão resolvidas; nenhum
`NEEDS CLARIFICATION` restante.
