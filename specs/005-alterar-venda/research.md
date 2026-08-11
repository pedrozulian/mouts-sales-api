# Research: Alterar Venda

Nenhum `NEEDS CLARIFICATION` restou no Technical Context do `plan.md` — a documentação DDD
(Notion), a sessão de `/speckit-clarify` da spec e o código já entregue pelas features 002, 003
e 004 fixam a maior parte das escolhas. Este documento registra as decisões técnicas
específicas desta feature.

## 1. Reconciliação centralizada em `Sale.Update` (two-pass validar-então-mutar)

**Decision**: `Sale.Update(customer, branch, saleDate, items)` retorna `Result<Sale>` e segue o
mesmo padrão de duas passagens já usado por `Sale.Create`/`ValidateAndCreateItems`: primeiro
valida cabeçalho e cada item do pedido (sem mutar o agregado), acumulando todos os erros em uma
lista; só quando não há nenhum erro é que o cabeçalho é atualizado, os itens existentes são
mutados (`SaleItem.ApplyChange`), os novos são adicionados, os ausentes são cancelados
(`SaleItem.Cancel`), o total é recalculado e os eventos (`ItemCancelled`×N + `SaleModified`)
são registrados.

**Rationale**: garante que uma requisição inválida nunca deixa o agregado em estado
parcialmente alterado (Princípio VII — nenhuma persistência quando o `Result` é falha, FR-017);
reaproveita um padrão já validado e testado por `Sale.Create`, em vez de introduzir uma segunda
estratégia de validação no mesmo agregado.

**Alternatives considered**:
- **Mutar e reverter em caso de erro (rollback manual)**: mais complexo, exigiria capturar o
  estado anterior de cada item antes de qualquer mutação; rejeitado — o two-pass evita mutação
  especulativa por completo.
- **Validar item a item e aplicar cada um imediatamente**: geraria uma reconciliação
  parcialmente aplicada quando um item no meio da lista falhasse; rejeitado por violar o
  requisito de atomicidade (FR-017, INV consistency).

## 2. Distinção `404` vs `400` a partir de um único `Result<Sale>`

**Decision**: `UpdateSaleCommandHandler` usa a mesma chave de erro `"id"` já estabelecida por
`GetSaleQueryHandler` (`003-consultar-venda`) exclusivamente para "venda não encontrada". Toda
outra falha de `Sale.Update` usa uma chave diferente da string exata `"id"` (`"sale"`,
`"saleDate"`, `"customer"`, `"branch"`, `"items"`, `"items[{i}].quantity"`,
`"items[{i}].product.id"`, `"items[{i}].id"`) — as chaves de item terminam em `.id` mas nunca
são iguais a `"id"` sozinho, então não colidem com a checagem abaixo. O endpoint decide o
status HTTP verificando se algum erro tem `Key == "id"` (igualdade exata, não `Contains`/
`EndsWith`): se sim, `404`; senão, `400`.

**Rationale**: evita introduzir um tipo de retorno paralelo (ex.: `Result<T>` com uma flag
`NotFound` explícita) só para esta feature — reaproveita uma convenção já em produção e mantém
`UpdateSaleCommandHandler` simétrico a `GetSaleQueryHandler` na forma de sinalizar ausência do
recurso.

**Alternatives considered**:
- **Handler dedicado para checar existência antes de chamar `Sale.Update`, retornando um tipo
  de erro próprio (`NotFoundResult`)**: mais explícito, mas duplicaria o conceito de resultado
  já coberto por `Result`/`Notification`; rejeitado por introduzir uma segunda forma de sinalizar
  falha no mesmo fluxo.
- **Endpoint distinguir por mensagem de texto**: frágil a mudanças de copy; rejeitado em favor
  da chave estruturada, que já é o campo usado para roteamento de erro em toda a API.

## 3. Carregamento com tracking (`Include`, sem `AsNoTracking`)

**Decision**: `UpdateSaleCommandHandler` carrega a venda via
`_context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == request.Id,
cancellationToken)` — **sem** `.AsNoTracking()`, ao contrário de `GetSaleQueryHandler`.

**Rationale**: é uma operação de escrita — o `ChangeTracker` do EF Core precisa detectar as
mutações aplicadas pelo agregado (`SaleItem.ApplyChange`, `SaleItem.Cancel`, novos itens
adicionados à coleção, mudança de cabeçalho) para gerar o `UPDATE`/`INSERT` correto em
`SaveChangesAsync`, sem que o handler precise montar SQL manualmente.

**Alternatives considered**: nenhuma — é a única forma padrão de EF Core suportar mutação de um
grafo de entidades já carregado sem reimplementar o rastreamento manualmente.

## 4. Encapsulamento: `SaleItem` só é mutado através de `Sale`

**Decision**: `SaleItem.ApplyChange(int quantity, decimal unitPrice)` e `SaleItem.Cancel()` são
métodos de instância que revalidam a quantidade (1 a 20) e recalculam desconto/total sempre que
chamados — mas só são invocados de dentro de `Sale.Update`, nunca diretamente pela Application.

**Rationale**: mantém `Sale` como a única porta de entrada do agregado (Princípio I, seção 2 da
documentação de Domain Model do Notion — "`SaleItem` nunca é criado, alterado ou cancelado por
fora"), garantindo que o total da venda e os eventos de domínio nunca fiquem dessincronizados de
uma mutação de item.

**Alternatives considered**: expor `ApplyChange`/`Cancel` como `internal` para reforçar o
encapsulamento em nível de compilador — considerado, mas rejeitado por ora para manter
consistência com a visibilidade `public` já usada por `SaleItem.Create` (chamado por `Sale`,
mas tecnicamente público); a garantia arquitetural já documentada e testada é suficiente para o
escopo deste protótipo.

## 5. Novos eventos de domínio: `SaleModified` e `ItemCancelled`

**Decision**: dois novos tipos em `SalesApi.Domain.Sales.Events`, herdando de `DomainEvent`
(igual a `SaleCreated`):
- `SaleModified(Guid SaleId, string SaleNumber, decimal TotalAmount)` — um único evento por
  alteração bem-sucedida, independentemente de quantos itens mudaram (FR-016).
- `ItemCancelled(Guid SaleId, Guid SaleItemId, Guid ProductId, int Quantity)` — um evento por
  item cancelado implicitamente pela reconciliação (FR-015).

Ambos acumulados no agregado `Sale` via `AddDomainEvent` (mesma mecânica de `SaleCreated`) e
despachados pelo `AppDbContext.SaveChangesAsync` já genérico — nenhuma mudança de
infraestrutura necessária. Dois novos `INotificationHandler` na Application
(`SaleModifiedEventHandler`, `ItemCancelledEventHandler`) apenas registram log estruturado via
Serilog, no mesmo formato de `SaleCreatedEventHandler`.

**Rationale**: os payloads seguem exatamente a tabela "Eventos de domínio" da documentação de
Domain Model do Notion; reaproveitar a mecânica de despacho existente evita qualquer alteração
em `AppDbContext`.

**Alternatives considered**: nenhuma — o mecanismo de despacho já é genérico o suficiente para
qualquer novo `DomainEvent` adicionado ao agregado.

## 6. Nenhuma migration EF Core necessária

**Decision**: a reconciliação só grava em colunas já existentes desde a migration `CreateSales`
(`002-registrar-venda`): `sales.customer_id/name`, `sales.branch_id/name`, `sales.sale_date`,
`sales.total_amount`, `sales.updated_at`, e em `sale_items`: `quantity`, `unit_price`,
`discount_percentage`, `discount_amount`, `total_amount`, `is_cancelled`. Novos itens
adicionados pela reconciliação são simples `INSERT` em `sale_items`, já cobertos pelo
`uq_sale_product` existente (que também reforça a INV-03 no banco). Nenhuma migration nova é
criada por esta feature.

**Rationale**: diferente de `004-listar-vendas` — que precisou materializar índices já previstos
no Notion, mas ainda ausentes do código —, o modelo de persistência para alteração não exige
nenhum ajuste de schema; a tabela já foi desenhada para suportar as colunas `is_cancelled` e
`updated_at` desde o registro.

**Alternatives considered**: nenhuma — não há necessidade de mudança de schema.

## 7. Reaproveitamento de DTOs de resposta e mapeamento Mapster

**Decision**: nenhuma alteração em `SaleResponse`, `SaleItemResponse` ou
`ExternalReferenceResponse`. A alteração só introduz DTOs do lado da requisição
(`UpdateSaleRequest`, `SaleItemChangeRequest`) e reaproveita o mapeamento
`Sale → SaleResponse` / `SaleItem → SaleItemResponse` já registrado globalmente por
`CreateSaleMappingConfig` — o mesmo padrão já usado por `003-consultar-venda`.

**Rationale**: a resposta de sucesso (`200`) deve ter exatamente o mesmo formato da consulta
(UC-02), conforme a spec (FR-014) — reaproveitar o mapeamento existente garante isso sem
duplicação.

**Alternatives considered**: criar um `UpdateSaleMappingConfig` que também remapeasse
`Sale → SaleResponse` — rejeitado por redundante; o registro já cobre exatamente esses tipos.

## 8. Testes para reconciliação envolvendo itens já cancelados

**Decision**: diferente de `003-consultar-venda` (que precisou preparar estado cancelado
diretamente no banco, já que `Sale` só expunha `Create`), os testes desta feature podem exercer
"referenciar um item já cancelado" de ponta a ponta usando apenas os métodos de domínio já
existentes após esta feature: `Sale.Create` seguido de uma primeira chamada a `Sale.Update` que
cancela um item implicitamente (omitindo-o do corpo), e então uma segunda chamada a
`Sale.Update` referenciando o `id` desse item já cancelado, esperando falha.

**Rationale**: esta é a própria feature que introduz o mecanismo de cancelamento implícito de
item — não há mais necessidade do workaround de seed direto no banco usado por
`003-consultar-venda`.

**Alternatives considered**: nenhuma — o mecanismo natural já é suficiente.

**Output**: todas as decisões técnicas necessárias para o desenho estão resolvidas; nenhum
`NEEDS CLARIFICATION` restante.
