# Research: Listar Vendas

Nenhum `NEEDS CLARIFICATION` restou no Technical Context do `plan.md` — a documentação DDD
(Notion), a sessão de `/speckit-clarify` (desempate de ordenação) e o código já entregue pelas
features 002/003 fixam a maior parte das escolhas. Este documento registra as decisões técnicas
específicas desta feature.

## 1. Validação e parsing manual dos parâmetros de query (em vez de binding nativo tipado)

**Decision**: o endpoint aceita `page`, `pageSize`, `customerId`, `branchId` e `isCancelled`
como `string?` brutos (sem tipagem `int?`/`Guid?`/`bool?` na assinatura do Minimal API), e
repassa esses valores como estão para `ListSalesQuery`. Todo parsing e validação — formato e
faixa de valor — acontece dentro de `ListSalesQueryHandler`, retornando `Result.Failure` com um
`Notification` por parâmetro inválido (`key` = nome do parâmetro), da mesma forma como
`Sale.Create` acumula múltiplos erros de validação antes de retornar.

**Rationale**: a spec (FR-011/FR-012) exige que **todo** parâmetro malformado — não só os fora
de faixa (`page`/`pageSize`) — responda com o mesmo contrato de erro
(`{ "errors": [{ "key", "message" }] }`) usado pelos demais casos de uso. Se o endpoint
declarasse `Guid? customerId` ou `bool? isCancelled` diretamente, o ASP.NET Core Minimal API
tentaria fazer o parsing durante o model binding; quando o valor informado não é conversível
(ex.: `customerId=abc`, `isCancelled=talvez`), a falha de binding produz um `400` **antes** do
handler ser chamado, com o corpo padrão do framework (`ProblemDetails`), não o envelope
`errors` da aplicação — quebrando FR-012 exatamente para esses casos.

**Alternatives considered**:
- **Tipar os parâmetros nativamente (`int? page`, `Guid? customerId`, `bool? isCancelled`) e
  aceitar o `400` padrão do framework para erros de formato**: mais simples e com menos código,
  mas viola FR-012 para os casos de `customerId`/`branchId`/`isCancelled` malformados — a spec
  é explícita ao exigir o mesmo contrato de erro em todos os casos, então foi rejeitada.
- **`[AsParameters]` com um record tipado**: mesmo problema da alternativa anterior, apenas
  reorganiza a assinatura do endpoint sem mudar onde o parsing falha.
- **FluentValidation para os parâmetros**: adicionaria uma dependência nova, fora da Stack
  Tecnológica Obrigatória da constitution (que exigiria emenda para ser adotada) só para um
  volume pequeno de regras simples (faixa numérica e formato); rejeitada por desproporcional ao
  problema.

## 2. Contagem e paginação no banco (duas consultas: `CountAsync` + `Skip`/`Take`)

**Decision**: `ListSalesQueryHandler` monta um único `IQueryable<Sale>` com os filtros
condicionais aplicados, executa `CountAsync` para obter `TotalCount` e, em seguida, aplica
`OrderByDescending(SaleDate).ThenByDescending(Id).Skip(...).Take(...)` sobre o mesmo
`IQueryable` para buscar a página. São duas idas ao banco, ambas geradas pelo EF Core a partir
da mesma árvore de filtros.

**Rationale**: é o padrão idiomático e mais legível para paginação com EF Core; ambas as
consultas usam os mesmos `Where` (filtros por cliente/filial/cancelamento), então os índices
adicionados nesta feature (seção 5) beneficiam as duas. Para o volume de dados de um protótipo
de avaliação técnica, o custo de uma consulta `COUNT` adicional é desprezível frente ao ganho de
simplicidade e clareza do código.

**Alternatives considered**:
- **Uma única consulta com window function (`COUNT(*) OVER()`)**: evitaria a segunda ida ao
  banco, mas exigiria SQL bruto ou uma projeção mais complexa fora do vocabulário LINQ comum do
  restante do projeto; rejeitada por complexidade desproporcional ao ganho de performance neste
  contexto.
- **Carregar tudo em memória e paginar com LINQ-to-Objects**: violaria a premissa de que a
  listagem não deve carregar a tabela inteira; rejeitada.

## 3. Projeção direta para `SaleSummaryResponse` via Mapster `ProjectToType`

**Decision**: a página de resultados é obtida com
`.ProjectToType<SaleSummaryResponse>().ToListAsync(cancellationToken)` sobre o `IQueryable<Sale>`
já filtrado, ordenado e paginado — não com `.Include(s => s.Items)` seguido de
`.Adapt<SaleSummaryResponse>()` em memória (padrão usado por `GetSaleQueryHandler` em
`003-consultar-venda`).

**Rationale**: FR-004 exige explicitamente que a listagem **não** retorne a coleção `Items`.
Usar `ProjectToType` faz o Mapster gerar uma expressão `Select` traduzida pelo provider do EF
Core para SQL, de forma que a consulta nunca sequer lê as linhas de `sale_items` — mais
eficiente do que carregar a entidade completa (com `Items`) e descartar a coleção depois de
mapear. Exige um `NewConfig<Sale, SaleSummaryResponse>()` explícito (`ListSalesMappingConfig`)
para o Mapster saber projetar exatamente os campos do resumo, incluindo o achatamento de
`Customer`/`Branch` (`ExternalReference` → `ExternalReferenceResponse`, mapeamento já registrado
por `CreateSaleMappingConfig` e reaproveitado aqui).

**Alternatives considered**:
- **`Include(Items)` + `Adapt` em memória, como em `003-consultar-venda`**: mais simples de
  escrever, mas contraria FR-004 ao custo de banda de rede/IO desnecessário — a query traria os
  itens do banco só para descartá-los depois do mapeamento; rejeitada.
- **`Select` manual (LINQ puro) montando `SaleSummaryResponse` sem Mapster**: funciona, mas
  duplicaria a forma do DTO na query LINQ, divergindo do padrão já estabelecido de reaproveitar
  configs do Mapster para toda projeção `Sale → *Response`; rejeitada por consistência com
  `003-consultar-venda` (seção 3 do research daquela feature).

## 4. Filtros combináveis como `Where` condicionais encadeados

**Decision**: cada filtro (`customerId`, `branchId`, `isCancelled`) só adiciona uma cláusula
`Where` ao `IQueryable<Sale>` quando o valor correspondente foi informado e é válido; múltiplos
filtros se combinam automaticamente por `AND` ao serem encadeados (FR-009).

**Rationale**: é a forma mais direta e legível de expressar "filtro opcional e cumulativo" em
LINQ/EF Core, sem precisar de biblioteca de especificação dinâmica ou `Expression` manual.

**Alternatives considered**: `Specification pattern` dedicado — útil se o número de filtros
combináveis crescesse muito, mas desproporcional para três filtros simples; rejeitado por
complexidade desnecessária (constitution exige justificar complexidade adicional).

## 5. Índices em `sales(customer_id)`, `sales(branch_id)` e `sales(sale_date)`

**Decision**: `SaleConfiguration` passa a declarar `HasIndex` para as colunas `customer_id`,
`branch_id` (dentro dos `OwnsOne` de `Customer`/`Branch`) e `sale_date`; uma nova migration
(`AddSalesListIndexes`) aplica essas alterações ao schema já existente.

**Rationale**: a seção "Modelo de persistência" da documentação DDD do Notion já especifica
`ix_sales_customer_id`, `ix_sales_branch_id` e `ix_sales_sale_date` — mas a migration inicial
(`CreateSales`, feature 002) só criou o índice único de `SaleNumber`, já que UC-01 não consulta
por esses campos. Esta é a primeira feature que efetivamente filtra e ordena por eles em uma
consulta que pode varrer toda a tabela; adicionar os índices agora evita table scan à medida que
o volume de vendas cresce, sem custo de design adicional (o modelo já previa isso).

**Alternatives considered**: não adicionar índices agora e tratar como otimização futura —
rejeitada porque o próprio Notion já define esse índice como parte do modelo de persistência da
feature de listagem, e adiar geraria retrabalho de migration mais tarde sem necessidade.

## 6. Testes para o filtro `isCancelled` (User Story 2)

**Decision**: como o cancelamento de vendas (UC-05/UC-06) ainda não foi implementado, os testes
desta feature que exercem `isCancelled=true`/`isCancelled=false` preparam o estado diretamente
no banco durante o Arrange (mesmo padrão descrito em `003-consultar-venda/research.md`, seção
6), sem passar por um método de domínio que ainda não existe.

**Rationale**: a listagem (UC-03) deve validar que filtra corretamente um estado já persistido,
independentemente de qual feature futura (UC-05/UC-06) produzirá esse estado em produção.
Bloquear os testes desta feature até UC-05/UC-06 existirem atrasaria desnecessariamente a
entrega do UC-03.

**Alternatives considered**: idênticas às já descritas e rejeitadas em
`003-consultar-venda/research.md` (adiar User Story ou antecipar métodos de cancelamento no
agregado); mesma decisão, por consistência entre features.

## 7. Cálculo de `TotalPages` e caso de zero registros

**Decision**: `PagedResult<T>.Create(items, page, pageSize, totalCount)` calcula
`TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)`.

**Rationale**: zero registros deve produzir `totalPages = 0` (não `1`), para o cliente da API
distinguir claramente "nenhum resultado" de "uma página com zero itens por acaso" — consistente
com FR-010/SC-003 (lista vazia com metadados de paginação indicando zero registros).

**Alternatives considered**: `TotalPages = 1` mesmo com zero registros — rejeitada por ser menos
informativa para o cliente decidir se deve ou não tentar paginar.

**Output**: todas as decisões técnicas necessárias para o desenho estão resolvidas; nenhum
`NEEDS CLARIFICATION` restante.
