# Research: Cancelar Item da Venda

Nenhum `NEEDS CLARIFICATION` restou no Technical Context do `plan.md` — a documentação DDD
(Notion), a spec (sem sessão de `/speckit-clarify`, pois o prompt de entrada já trazia as regras
resolvidas) e o código já entregue pelas features 002, 003, 005 e 006 fixam a maior parte das
escolhas. Este documento registra as decisões técnicas específicas desta feature.

## 1. `Sale.CancelItem(Guid itemId)` — reaproveita `Sale.Cancel()` para a cascata (FR-009/FR-012)

**Decision**: `Sale.CancelItem(itemId)` faz sua própria validação e mutação (verificar
imutabilidade da venda, localizar o item, verificar se já está cancelado, cancelar o item,
recalcular o total). Ao final, se `_items.All(i => i.IsCancelled)` for verdadeiro, delega para
`Cancel()` — já existente desde a feature 006 — em vez de duplicar a lógica de "marcar a venda
como cancelada e emitir `SaleCancelled`".

```csharp
public Result<Sale> CancelItem(Guid itemId)
{
    if (IsCancelled)
    {
        return Result<Sale>.Failure(new Notification("sale", "Venda cancelada não pode ter itens cancelados."));
    }

    var item = _items.FirstOrDefault(i => i.Id == itemId);

    if (item is null)
    {
        return Result<Sale>.Failure(new Notification("itemId", "Item não encontrado nesta venda."));
    }

    if (item.IsCancelled)
    {
        return Result<Sale>.Failure(new Notification("item", "Item já está cancelado."));
    }

    item.Cancel();
    TotalAmount = _items.Where(i => !i.IsCancelled).Sum(i => i.TotalAmount);
    UpdatedAt = DateTime.UtcNow;

    AddDomainEvent(new ItemCancelled(Id, item.Id, item.Product.Id, item.Quantity));

    return _items.All(i => i.IsCancelled) ? Cancel() : Result<Sale>.Success(this);
}
```

Quando a cascata se aplica, `Cancel()` roda com todos os itens já cancelados — seu laço
`_items.Where(i => !i.IsCancelled)` não encontra nada para iterar (idempotente), mas ainda marca
`IsCancelled = true`, reafirma `TotalAmount = 0m` (já era `0m`, redundante e inofensivo),
atualiza `UpdatedAt` novamente e emite `SaleCancelled`.

**Rationale**: `Cancel()` já é a única fonte de verdade para "o que significa uma venda estar
cancelada" (INV-07, `IsCancelled = true`, `TotalAmount = 0m`, evento `SaleCancelled`). Duplicar
esses três passos dentro de `CancelItem` criaria dois lugares que precisam evoluir juntos —
violação de DRY e risco direto ao Princípio III (SOLID/SRP: `Cancel()` deixaria de ser a única
responsável por essa transição de estado).

**Alternatives considered**:
- **Duplicar a lógica de cascata dentro de `CancelItem`** (`IsCancelled = true;
  AddDomainEvent(new SaleCancelled(...))` inline): rejeitada — exatamente o problema que a reutilização evita; um ajuste futuro em `Cancel()` (ex.: novo campo de auditoria) poderia divergir silenciosamente do comportamento da cascata.
- **Extrair um método privado comum (`MarkAsCancelled()`) chamado por ambos `Cancel()` e
  `CancelItem()`**: equivalente em efeito a reaproveitar `Cancel()` diretamente, mas adiciona uma
  indireção sem benefício — `Cancel()` já é público, idempotente o suficiente para este uso, e
  testado desde a feature 006.

## 2. Itens já cancelados individualmente permanecem intocáveis (INV-08/FR-008)

**Decision**: `CancelItem` rejeita com `400` (chave `"item"`) quando o item alvo já está
cancelado, sem alterar `TotalAmount` nem emitir qualquer evento — mesmo padrão de `Sale.Cancel()`
rejeitando uma venda já cancelada (chave `"sale"`).

**Rationale**: leitura direta de FR-008; reforça a leitura da spec (User Story 3, cenário 4) e
evita eventos duplicados para uma operação sem efeito observável.

**Alternatives considered**: nenhuma — é a leitura direta e única de FR-008.

## 3. Duas chaves de erro distintas para "não encontrado" (`itemId`) vs. "regra de negócio" (`item`)

**Decision**: a convenção já usada para a venda (`"id"` = venda não encontrada → `404`; `"sale"` =
regra de negócio violada → `400`, estabelecida em 005/006) é estendida ao item: `"itemId"` = item
não encontrado ou não pertence à venda → `404`; `"item"` = item já cancelado → `400`. O endpoint
decide o status HTTP verificando `error.Key == "id" || error.Key == "itemId"` para `404`, caso
contrário `400` — mesma mecânica de `UpdateSale`/`CancelSale`, apenas com uma chave adicional.

**Rationale**: sem essa distinção, "item não encontrado" e "item já cancelado" cairiam na mesma
chave e o endpoint não conseguiria diferenciar `404` de `400` — ambos FR-007 (não encontrado) e
FR-008 (já cancelado) exigem status HTTP diferentes (ver `spec.md`, User Story 3, cenários 3 e 4).

**Alternatives considered**:
- **Uma única chave `"item"` para os dois casos, com o endpoint inspecionando o texto da
  mensagem**: rejeitada — acopla a decisão de roteamento HTTP ao conteúdo textual da mensagem de
  erro, frágil a qualquer ajuste de copy futuro.

## 4. Concorrência: reaproveita o token `xmin` já mapeado em `SaleConfiguration` (006) — nenhuma mudança de Infrastructure (FR-015)

**Decision**: nenhuma alteração em `SaleConfiguration`. `CancelItem` sempre grava
`TotalAmount`/`UpdatedAt` na própria linha de `Sale` (e, na cascata, também `IsCancelled`) — a
mesma linha já protegida pelo token de concorrência `xmin` introduzido pela feature 006 para
`Sale.Cancel()`/`Sale.Update()`. Quando duas requisições concorrentes carregam a mesma `Sale`,
cancelam o mesmo item e tentam `SaveChangesAsync`, a segunda encontra um `xmin` divergente do que
leu e o EF Core lança `DbUpdateConcurrencyException` — capturada em
`CancelSaleItemCommandHandler` e traduzida para `Result.Failure(new Notification("item", "Item já
está cancelado."))`, a mesma chave/mensagem já usada para o cancelamento sequencial de um item já
cancelado (seção 2). O chamador que perdeu a corrida recebe `400`.

**Rationale**: como `SaleItem` não tem chave primária própria mapeada como token de concorrência,
mas toda escrita em `CancelItem` sempre toca a linha de `Sale` (recalcula `TotalAmount`), o token
já existente é suficiente — não há necessidade de mapear um segundo token em `sale_items`.
Confirma a decisão já registrada em `specs/006-cancelar-venda/research.md`, seção 3 ("o token de
concorrência se aplica a toda escrita em `Sale`, não só ao cancelamento").

**Alternatives considered**:
- **Token de concorrência adicional em `SaleItem` (`xmin` da tabela `sale_items`)**: rejeitada —
  redundante; qualquer escrita em um item sempre passa por uma escrita na `Sale` (recálculo de
  `TotalAmount`), então o token existente já detecta o conflito sem precisar de um segundo
  mecanismo.
- **Mensagem genérica de conflito, sem tentar apontar a causa mais provável**: rejeitada por
  consistência — os dois handlers de escrita já existentes (`CancelSaleCommandHandler`,
  `UpdateSaleCommandHandler`) usam uma mensagem fixa mapeada para o cenário mais provável de
  conflito, sem reconsultar o estado após a exception; `CancelSaleItemCommandHandler` segue a
  mesma convenção para não introduzir um padrão de tratamento de erro divergente dentro do mesmo
  projeto.
- **Reconsultar o estado persistido (sem tracking) após capturar a exception, para escolher entre
  "venda já cancelada" e "item já cancelado" com precisão**: seria mais preciso quando a corrida
  concorrente é, na verdade, contra um `DELETE /api/sales/{id}` (006) em vez de contra o mesmo
  item — mas adicionaria uma consulta extra só no caminho de exceção para um cenário de corrida
  entre duas features diferentes, sem nenhum FR exigindo essa precisão adicional (a spec, seção
  Assumptions, só exige que a requisição perdedora receba `400`, não uma mensagem
  perfeitamente atribuída à causa). Rejeitada por complexidade desnecessária, mantendo o padrão
  já validado no projeto.

## 5. Nenhum event handler novo — `ItemCancelled` e `SaleCancelled` já existem

**Decision**: `AddDomainEvent(new ItemCancelled(...))` e, quando a cascata se aplica,
`AddDomainEvent(new SaleCancelled(...))` (via `Cancel()`) reaproveitam exatamente os eventos e os
handlers (`ItemCancelledEventHandler`, `SaleCancelledEventHandler`) já implementados pelas
features 005 e 006 — nenhuma classe nova em `SalesApi.Domain.Sales.Events` nem em
`SalesApi.Application.Sales.Events`.

**Rationale**: os dois eventos já têm exatamente o payload que esta feature precisa
(`ItemCancelled`: `SaleId`, `SaleItemId`, `ProductId`, `Quantity`; `SaleCancelled`: `SaleId`,
`SaleNumber`) — introduzidos antes de existir um caminho de código que os emitisse em conjunto na
mesma operação, mas o mecanismo de despacho (`AppDbContext.SaveChangesAsync`) já suporta múltiplos
eventos acumulados no mesmo agregado sem nenhuma mudança.

**Alternatives considered**: nenhuma — é a leitura direta da tabela "Eventos de domínio" do Domain
Model do Notion, já implementada.

## 6. Carregamento com tracking (`Include`, sem `AsNoTracking`)

**Decision**: `CancelSaleItemCommandHandler` carrega a venda via
`_context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == request.SaleId,
cancellationToken)` — sem `.AsNoTracking()`, mesmo padrão de `CancelSaleCommandHandler` e
`UpdateSaleCommandHandler`.

**Rationale**: é uma operação de escrita — o `ChangeTracker` do EF Core precisa detectar as
mutações aplicadas pelo agregado (`IsCancelled`/`TotalAmount` do item e da venda) para gerar o
`UPDATE` correto em `SaveChangesAsync`.

**Alternatives considered**: nenhuma — mesma justificativa de 005/006.

## 7. Resposta sem corpo: `Result` não genérico em vez de `Result<SaleResponse>`

**Decision**: `CancelSaleItemCommand` implementa `IRequest<Result>` (não
`IRequest<Result<SaleResponse>>`) — mesmo padrão de `CancelSaleCommand` (006). O endpoint mapeia
sucesso para `Results.NoContent()` (`204`), sem serializar nenhum DTO.

**Rationale**: reflete exatamente o contrato do Notion (UC-06: sucesso é `204` sem corpo).

**Alternatives considered**:
- **Retornar `Result<SaleResponse>` e ignorar o valor no endpoint**: rejeitado — mesma razão já
  registrada em `specs/006-cancelar-venda/research.md`, seção 7.

## 8. Nenhuma migration EF Core necessária

**Decision**: o cancelamento de item só grava em colunas já existentes desde a migration
`CreateSales` (`is_cancelled`/`total_amount`, em `sales` e `sale_items`). O token de concorrência
(`xmin`) já foi mapeado pela feature 006 e é reaproveitado sem nenhuma alteração.

**Rationale**: mesma situação de `005-alterar-venda` e `006-cancelar-venda` — o modelo de
persistência já foi desenhado para suportar exatamente as colunas que esta feature precisa gravar.

**Alternatives considered**: nenhuma — não há necessidade de mudança de schema.

**Output**: todas as decisões técnicas necessárias para o desenho estão resolvidas; nenhum
`NEEDS CLARIFICATION` restante.
