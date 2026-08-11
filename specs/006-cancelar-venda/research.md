# Research: Cancelar Venda

Nenhum `NEEDS CLARIFICATION` restou no Technical Context do `plan.md` — a documentação DDD
(Notion), a sessão de `/speckit-clarify` da spec e o código já entregue pelas features 002, 003 e
005 fixam a maior parte das escolhas. Este documento registra as decisões técnicas específicas
desta feature.

## 1. `Sale.Cancel()` — método de escrita de passagem única (sem two-pass)

**Decision**: `Sale.Cancel()` não recebe nenhum parâmetro e retorna `Result<Sale>`. Ao contrário
de `Sale.Update` (dois passes: validar tudo, depois mutar — necessário porque recebe dados
externos por item), `Cancel()` só tem uma regra a checar (`IsCancelled`) e nenhum dado de entrada
para validar; por isso executa em uma única passagem: se a venda já está cancelada, retorna
`Failure` imediatamente; senão, cancela cada item ainda ativo (`SaleItem.Cancel()`), marca a
venda como cancelada, zera `TotalAmount`, atualiza `UpdatedAt` e registra `SaleCancelled`.

**Rationale**: não há entrada externa para este caso de uso além do identificador da venda (já
usado para carregá-la) — introduzir o padrão two-pass de `Update` seria complexidade sem
propósito, já que não existe nenhum cenário em que uma mutação precise ser desfeita a meio
caminho.

**Alternatives considered**:
- **Reaproveitar `Sale.Update` com uma flag `cancel: true`**: rejeitado — misturaria dois
  contratos de escrita bem diferentes (reconciliação completa de itens vs. cancelamento em
  massa) em um único método, prejudicando a legibilidade e o Princípio III (SRP).

## 2. Itens já cancelados individualmente permanecem inalterados (FR-012)

**Decision**: `Sale.Cancel()` itera apenas sobre `_items.Where(i => !i.IsCancelled)` — um item já
cancelado antes do cancelamento da venda (por exemplo, por um cancelamento de item futuro, UC-06)
não sofre nenhuma mutação nem gera efeito colateral adicional.

**Rationale**: reforça a leitura da spec (User Story 1, cenário 3) e evita reemitir estado ou
custo de mutação para algo que já é verdade.

**Alternatives considered**: nenhuma — é a leitura direta e única de FR-012.

## 3. Concorrência: `xmin` do PostgreSQL como token de concorrência otimista do EF Core (FR-013)

**Decision**: `SaleConfiguration.Configure` passa a chamar
`builder.Property<uint>("xmin").IsRowVersion()` — forma padrão do EF Core para mapear a coluna de
sistema `xmin` (presente em toda tabela PostgreSQL, incrementada a cada `UPDATE` da linha) como
token de concorrência. O método de conveniência `UseXminAsConcurrencyToken()`, oferecido pelo
provider `Npgsql.EntityFrameworkCore.PostgreSQL`, está marcado obsoleto na versão em uso (8.0.11)
em favor exatamente desta forma padrão — descoberto ao compilar, já corrigido antes do primeiro
commit desta feature. Nenhuma coluna nova, nenhuma migration. Quando duas requisições
concorrentes carregam a mesma `Sale`, cancelam e tentam `SaveChangesAsync`, a segunda encontra um
`xmin` divergente do que leu e o EF Core lança `DbUpdateConcurrencyException` — capturada em
`CancelSaleCommandHandler` e traduzida para `Result.Failure(new Notification("sale", "Venda já
está cancelada."))`, a mesma chave/mensagem já usada para o cancelamento sequencial de uma venda
já cancelada. O chamador que perdeu a corrida recebe `400`, exatamente como decidido na sessão de
`/speckit-clarify` (Option A).

**Rationale**: é a única forma de satisfazer FR-013 sem introduzir infraestrutura nova (fila,
lock distribuído, `SELECT ... FOR UPDATE` explícito) — o projeto já usa EF Core/PostgreSQL, e o
provider Npgsql oferece esse mecanismo pronto, sem custo de schema.

**Alternatives considered**:
- **Coluna `row_version`/`xmin` explícita (`byte[]`/`uint`) com `IsRowVersion()`**: equivalente
  em efeito ao `xmin` nativo, mas exigiria uma migration nova e uma coluna redundante quando o
  PostgreSQL já expõe exatamente essa informação de graça; rejeitada por complexidade
  desnecessária.
- **`SELECT ... FOR UPDATE` explícito (lock pessimista) na query de carregamento**: garante o
  mesmo resultado, mas serializa até leituras que não terminariam em escrita concorrente
  (`GetSaleQuery`, se reaproveitasse a mesma query), e exige SQL bruto ou configuração adicional
  no EF Core; rejeitada por ser mais invasiva que o token otimista para um protótipo de baixa
  concorrência real.
- **Ignorar FR-013 e aceitar corrida silenciosa**: rejeitada — foi explicitamente descartada na
  sessão de clarificação da spec (Option B, idempotência silenciosa).

**Efeito colateral aceito**: o token de concorrência se aplica a toda escrita em `Sale`, não só
ao cancelamento — `UpdateSaleCommandHandler` (005) passa a poder receber a mesma
`DbUpdateConcurrencyException` quando um `PUT` concorre com um `DELETE` da mesma venda. Por isso
esta feature também adiciona a mesma captura ali (ver seção 4), para não deixar uma exception não
tratada vazar como `500` em um cenário de negócio esperado.

## 4. Ajuste mínimo em `UpdateSaleCommandHandler` (005) para manter o Princípio VII

**Decision**: `UpdateSaleCommandHandler.Handle` envolve `await _context.SaveChangesAsync(...)` em
um `try/catch (DbUpdateConcurrencyException)`, retornando `Result<SaleResponse>.Failure(new
Notification("sale", "Venda cancelada não pode ser alterada."))` — a mesma mensagem já usada
quando a venda já está cancelada no início do `Sale.Update`, já que o efeito observável para o
chamador é idêntico (a venda foi cancelada por outra requisição entre a leitura e a escrita).

**Rationale**: sem este ajuste, o token de concorrência introduzido na seção 3 tornaria um
cenário de negócio válido (perder a corrida contra um cancelamento) em uma exception não tratada,
violando o Princípio VII (nenhuma exception vazando como erro de negócio) para um caminho que só
passa a existir por causa desta feature.

**Alternatives considered**:
- **Não ajustar `UpdateSaleCommandHandler`**: rejeitada — deixaria uma regressão de contrato de
  erro (`500` em vez de `400`) introduzida silenciosamente por esta feature.
- **Middleware global de tratamento de `DbUpdateConcurrencyException`**: mapearia a exception
  para `400` em qualquer handler, de forma genérica; rejeitada por ora — nenhum outro handler
  além de `Update` e `Cancel` escreve em `Sale`, então um middleware genérico seria complexidade
  antecipada para um problema que hoje só tem dois pontos de ocorrência conhecidos.

## 5. Distinção `404` vs `400` a partir de um único `Result` (reaproveitada de 005)

**Decision**: `CancelSaleCommandHandler` usa a mesma chave de erro `"id"` já estabelecida por
`GetSaleQueryHandler`/`UpdateSaleCommandHandler` exclusivamente para "venda não encontrada".
Venda já cancelada e conflito de concorrência usam a chave `"sale"`. O endpoint decide o status
HTTP verificando se algum erro tem `Key == "id"` — mesma convenção já usada por `UpdateSale`.

**Rationale**: mantém `CancelSaleCommandHandler` simétrico aos handlers de escrita já existentes,
sem introduzir uma segunda forma de sinalizar "recurso não encontrado".

**Alternatives considered**: nenhuma — reaproveita a convenção já validada por 005.

## 6. Carregamento com tracking (`Include`, sem `AsNoTracking`)

**Decision**: `CancelSaleCommandHandler` carrega a venda via
`_context.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == request.Id,
cancellationToken)` — sem `.AsNoTracking()`, mesmo padrão de `UpdateSaleCommandHandler`.

**Rationale**: é uma operação de escrita — o `ChangeTracker` do EF Core precisa detectar as
mutações aplicadas pelo agregado (`IsCancelled` da venda e de cada item, `TotalAmount` zerado)
para gerar o `UPDATE` correto em `SaveChangesAsync`.

**Alternatives considered**: nenhuma — mesma justificativa de 005 (research.md, seção 3).

## 7. Resposta sem corpo: `Result` não genérico em vez de `Result<SaleResponse>`

**Decision**: `CancelSaleCommand` implementa `IRequest<Result>` (não `IRequest<Result<SaleResponse>>`)
— a classe base `Result` já existe em `SalesApi.Domain.Common` justamente para casos sem valor de
retorno. O endpoint mapeia sucesso para `Results.NoContent()` (`204`), sem serializar nenhum DTO.

**Rationale**: reflete exatamente o contrato do Notion (UC-05: sucesso é `204` sem corpo) e evita
montar um `SaleResponse` que seria descartado pelo `NoContent()` — nenhum outro handler desta API
precisou disso até agora (`Create`/`Get`/`List`/`Update` sempre devolvem corpo), mas o tipo
`Result` de base já suporta o caso sem exigir nenhuma mudança em `SalesApi.Domain.Common`.

**Alternatives considered**:
- **Retornar `Result<SaleResponse>` e ignorar o valor no endpoint**: rejeitado — construiria e
  descartaria um DTO à toa, além de sugerir incorretamente que a resposta tem corpo.

## 8. Novo evento de domínio: `SaleCancelled`

**Decision**: `SaleCancelled(Guid SaleId, string SaleNumber)`, em `SalesApi.Domain.Sales.Events`,
herdando de `DomainEvent` (`OccurredOn` automático) — mesmo padrão de `SaleCreated`/`SaleModified`.
Acumulado via `AddDomainEvent` dentro de `Sale.Cancel()`, despachado pelo
`AppDbContext.SaveChangesAsync` já genérico. Um novo `SaleCancelledEventHandler` (Application)
apenas registra log estruturado via Serilog, no mesmo formato de `SaleModifiedEventHandler`.
Nenhum `ItemCancelled` é emitido pelo cancelamento em massa (FR-008/FR-009) — decisão já fixada
pela documentação do Notion e pela spec.

**Rationale**: payload segue exatamente a tabela "Eventos de domínio" do Domain Model do Notion
(`SaleId`, `SaleNumber`, `OccurredOn`); reaproveitar a mecânica de despacho existente evita
qualquer alteração em `AppDbContext`.

**Alternatives considered**: nenhuma — o mecanismo de despacho já é genérico o suficiente.

## 9. Nenhuma migration EF Core necessária

**Decision**: o cancelamento só grava em colunas já existentes desde a migration `CreateSales`
(`is_cancelled` e `total_amount`, em `sales` e `sale_items`). O token de concorrência (`xmin`) é
uma coluna de sistema do PostgreSQL, não requer nenhuma migration para ser mapeada pelo EF Core.

**Rationale**: mesma situação de `005-alterar-venda` — o modelo de persistência já foi desenhado
para suportar exatamente as colunas que esta feature precisa gravar.

**Alternatives considered**: nenhuma — não há necessidade de mudança de schema.

**Output**: todas as decisões técnicas necessárias para o desenho estão resolvidas; nenhum
`NEEDS CLARIFICATION` restante.
