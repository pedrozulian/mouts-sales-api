# Research: Confiabilidade Operacional e Consistência de Dados

Nenhum `NEEDS CLARIFICATION` restou no Technical Context do `plan.md` — as decisões de desenho
mais sensíveis desta feature já foram discutidas e fechadas com o usuário antes da redação da
spec. Este documento registra a decisão técnica de cada eixo, a alternativa descartada e o
motivo.

## 1. Provisionamento do schema: serviço `migrator` com migration bundle, não `Database.Migrate()`

**Decision**: adicionar um serviço `migrator` ao `docker-compose.yml`, construído a partir de um
stage novo do `Dockerfile` que gera um
[EF Core migration bundle](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#bundles)
(`dotnet ef migrations bundle --self-contained -r linux-x64`) — um executável autocontido que
aplica as migrations pendentes e termina. O serviço roda com `restart: "no"`; a `api` declara
`depends_on: { postgres: { condition: service_healthy }, migrator: { condition:
service_completed_successfully } }`. A ordem de subida passa a ser
`postgres (healthy) → migrator (completed) → api (start)`, e `docker compose up -d` continua
sendo o único comando que o usuário digita.

**Rationale**: satisfaz simultaneamente FR-001 (etapa própria), FR-002 (comando único), FR-003
(sem efeito colateral do startup) e FR-005 (aplicação não fica pronta sem schema aplicado) — sem
exigir que o usuário execute nenhum passo manual, e sem colocar DDL dentro do processo de runtime
da aplicação. O bundle não exige SDK nem `dotnet-ef` no ambiente de execução final — a imagem do
`migrator` carrega só o executável gerado, mesmo espírito de multi-stage já usado pelo Dockerfile
da `api`.

**Alternatives considered**:
- **`Database.Migrate()` em `Program.cs`**: mais simples (uma linha), mas viola FR-003
  diretamente e reproduz, em qualquer topologia com mais de uma réplica, os riscos discutidos
  com o usuário — corrida entre processos aplicando DDL simultaneamente (EF Core 8 não tem lock
  de migration distribuído entre processos; isso só chega no EF Core 9), exigência permanente de
  privilégio de DDL no usuário de runtime da aplicação, e acoplamento entre o tempo de boot da
  API e o tempo de uma migration lenta. Rejeitada mesmo sendo o caminho de menor esforço —
  registrada em `Complexity Tracking` do plano por alterar a topologia do ambiente.
- **Stage baseado no SDK completo rodando `dotnet ef database update`**: funciona, mas produz uma
  imagem do `migrator` sensivelmente mais pesada (carrega o SDK inteiro) só para aplicar
  migrations, e exige copiar o código-fonte do projeto de Infrastructure para dentro da imagem.
  Rejeitada em favor do bundle, que resolve o mesmo problema com uma imagem menor e sem SDK.
- **Script SQL idempotente (`dotnet ef migrations script --idempotent`) revisado manualmente**: é
  o padrão mais conservador para ambientes regulados, mas reintroduz exatamente o passo manual
  que FR-002 pede para eliminar. Registrado no `README.md` (US5) como o caminho recomendado para
  um ambiente produtivo real, sem ser a escolha para o ambiente de desenvolvimento/avaliação
  desta feature (ver Assumptions da spec).
- **Ferramenta de migration dedicada (Flyway/Liquibase)**: equivalente em efeito, mas introduz
  dependência nova fora da stack já definida pela constitution — rejeitada sem necessidade de
  abrir uma emenda para um ganho que o EF Core já entrega nativamente.

## 2. Health check: `IHealthCheck` customizado detectando migrations pendentes

**Decision**: `PendingMigrationsHealthCheck : IHealthCheck` chama
`await _context.Database.GetPendingMigrationsAsync(cancellationToken)` e retorna `Unhealthy`
quando a coleção não é vazia, com a lista de migrations pendentes na descrição. Registrado como
`services.AddHealthChecks().AddCheck<PendingMigrationsHealthCheck>("postgresql")` — mesmo nome
(`"postgresql"`) já usado pelo check existente, preservando o contrato de resposta atual
(FR-007) sem adicionar uma segunda entrada em `checks[]`. A verificação de conectividade
(`AddNpgSql`) é absorvida pelo próprio `GetPendingMigrationsAsync` — que já precisa abrir conexão
para consultar o catálogo de migrations aplicadas — eliminando a necessidade de duas dependências
fazendo papéis parcialmente redundantes.

**Rationale**: é o cenário exato que a investigação anterior expôs — `/health` respondendo
`200 Healthy` contra um banco cuja estrutura não existe, porque o check original só testava
`SELECT 1`. `GetPendingMigrationsAsync` é a API oficial do EF Core para essa pergunta e já é
usada pelo próprio `SalesApiFactory` nos testes de integração — não introduz conceito novo,
apenas o aplica também em runtime.

**Alternatives considered**:
- **Manter `AddNpgSql` e adicionar o novo check como uma segunda entrada em `checks[]`**:
  rejeitada — FR-007 exige preservar o contrato de resposta atual; duas entradas para a mesma
  dependência física confundiria o consumidor do health check (orquestrador, painel) sobre qual
  delas importa para decidir prontidão.
- **Verificar a versão da migration mais recente aplicada comparando com um valor fixo no
  código**: mais frágil — exigiria atualizar uma constante a cada nova migration; `dotnet ef`
  já mantém essa informação no catálogo `__EFMigrationsHistory`, e `GetPendingMigrationsAsync` já
  faz essa comparação corretamente.

## 3. Arredondamento monetário: no ponto de cálculo, não no ponto de exibição

**Decision**: `Math.Round(valor, 2, MidpointRounding.AwayFromZero)` é aplicado dentro de
`SaleItem` — no construtor privado e em `ApplyChange(quantity, unitPrice)` —, sobre
`DiscountAmount` e `TotalAmount`, imediatamente após o cálculo a partir de `Quantity` e
`UnitPrice`. `Sale.TotalAmount` continua sendo a soma simples dos `TotalAmount` dos itens ativos
— como esses já chegam arredondados, a soma não precisa de arredondamento adicional para
satisfazer FR-011 (total da venda igual à soma exata dos itens).

**Rationale**: arredondar no cálculo, e não na serialização de resposta (por exemplo, um
`[JsonConverter]` ou uma formatação no DTO), é o que garante FR-009 — o mesmo valor exato é
persistido, devolvido no `POST`/`PUT` e devolvido em qualquer `GET` subsequente. Arredondar só na
exibição deixaria o banco com o valor não arredondado, que é exatamente o defeito confirmado
(divergência entre a resposta de escrita em memória e a resposta de leitura pós-persistência).

**Alternatives considered**:
- **Arredondar apenas no total da venda (`Sale.TotalAmount`), deixando os itens com o valor
  cru**: rejeitada — o item, isoladamente, já é exibido com mais de duas casas na resposta
  (`discountAmount`, `totalAmount` por item), então o defeito persistiria no nível do item.
- **Arredondar na camada de mapeamento (Mapster, ao converter `SaleItem` → `SaleItemResponse`)**:
  rejeitada pelo mesmo motivo da alternativa de exibição — o valor persistido no banco
  continuaria não arredondado, violando FR-011 nos dados, não só na resposta.
- **Introduzir um value object `Money` com política de arredondamento embutida**: seria a
  evolução natural (já registrada como trade-off consciente no Guia Técnico do Notion), mas está
  fora do escopo desta feature (ver spec.md, Assumptions) — a correção pontual com `Math.Round`
  resolve o defeito confirmado sem introduzir um tipo novo no domínio.

## 4. Modelo físico: `HasColumnName` explícito, não `EFCore.NamingConventions`

**Decision**: cada propriedade mapeada em `SaleConfiguration` e `SaleItemConfiguration` recebe
`HasColumnName` explícito em snake_case (`sale_number`, `sale_date`, `total_amount`,
`is_cancelled`, `created_at`, `updated_at`, `quantity`, `unit_price`, `discount_percentage`,
`discount_amount`), incluindo a shadow property da FK
(`builder.Property<Guid>("SaleId").HasColumnName("sale_id")` — hoje não existe na classe
`SaleItem`, só na configuração). Índices e constraints ganham `HasDatabaseName` alinhado ao
Domain Model do Notion (`ix_sales_sale_number`, `ix_sale_items_sale_id`, `uq_sale_product`). Uma
migration nova (`RenameColumnsToSnakeCase`) usa `RenameColumn`/`RenameIndex` sobre as tabelas já
existentes — as duas migrations anteriores (`CreateSales`, `AddSalesListIndexes`) não são
alteradas nem substituídas.

**Rationale**: o modelo tem 2 entidades e ~20 colunas — o custo do mapeamento explícito é
trivial e cada linha é auditável por quem revisa o PR. Preserva a stack tecnológica definida pela
constitution sem abrir uma discussão de emenda (Assumptions da spec) e sem introduzir
comportamento implícito de uma convenção de terceiros sobre todo o modelo.

**Alternatives considered**:
- **`EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`)**: resolve o mesmo problema em
  uma linha, e é a escolha certa quando o modelo cresce (dezenas de entidades). Descartada aqui
  porque introduziria uma dependência nova para um ganho que, num modelo deste tamanho, o
  mapeamento explícito já entrega sem abrir a discussão de stack da constitution. Fica registrada
  como a opção a reconsiderar se o modelo de dados crescer substancialmente.
- **Reescrever (squash) as migrations `CreateSales` e `AddSalesListIndexes` para já nascerem em
  snake_case**: rejeitada — destruiria o histórico de evolução do schema que o próprio processo
  de desenvolvimento do projeto (uma migration por feature) documenta, e contradiz a prática já
  estabelecida nas features anteriores.

## 5. INV-03 considerando itens cancelados: domínio rejeita antes do banco

**Decision**: `Sale.ReconcileNewItem` (chamado por `Sale.Update` para itens sem `id`, isto é,
itens novos) passa a comparar o produto do item novo contra o conjunto de produtos de **todos**
os itens já pertencentes à venda — ativos e cancelados —, não apenas contra os produtos
referenciados no corpo da requisição atual. Ao detectar conflito, adiciona uma `Notification` com
a chave `items[{index}].product.id`, mesma convenção já usada para produto duplicado dentro do
próprio corpo. O índice único `(sale_id, product_id)` em `sale_items` permanece exatamente como
está — ele já impedia a duplicação física; o que faltava era o domínio recusar a tentativa antes
de chegar ao banco.

**Rationale**: é a leitura que preserva a INV-03 tal como documentada ("o mesmo produto aparece
no máximo uma vez por venda", sem qualificar "entre os ativos") e não exige alterar a restrição
de integridade que já a sustenta — apenas fecha o buraco de o domínio validar um subconjunto
menor do que o banco realmente impõe.

**Alternatives considered**:
- **Índice único parcial (`WHERE is_cancelled = false`)**: permitiria reintroduzir o produto de
  um item cancelado como item novo — comportamento plausível de negócio ("comprou de novo depois
  de cancelar"), mas diverge da leitura literal da INV-03 documentada e exigiria alterar uma
  restrição de banco já em produção conceitual (a que a spec 002 estabeleceu). Descartada nesta
  rodada; registrada como decisão consciente e revisável em `spec.md` (Assumptions), não como
  bug a corrigir depois.
- **Validar apenas na Application, antes de delegar ao domínio**: rejeitada — violaria o
  Princípio I (regra de negócio pertence ao Domain); a Application não tem, hoje, acesso direto
  aos itens da venda sem passar pelo agregado.

## 6. Contrato de erro: `IExceptionHandler` + `UseExceptionHandler`, não uma página de exceção

**Decision**: `GlobalExceptionHandler : IExceptionHandler` implementa `TryHandleAsync`, loga a
exceção original via `ILogger` (com o `CorrelationId` já presente no `LogContext` do middleware
existente) e escreve `{ "errors": [{ "key": "server", "message": "Ocorreu um erro inesperado." }] }`
com `500`, sempre — independentemente de `ASPNETCORE_ENVIRONMENT`. `Program.cs` registra
`builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();` e `app.UseExceptionHandler()` antes do middleware de
correlation id.

**Rationale**: `IExceptionHandler` (introduzido no .NET 8) é a forma recomendada atual — testável
por injeção de dependência, ao contrário de um lambda inline em `UseExceptionHandler(app => ...)`.
Aplicar sempre, e não só fora de `Development`, é o que FR-014 exige literalmente ("em nenhum
ambiente de execução") — decisão já validada com o usuário: o contrato de erro único é o
argumento que o próprio projeto usa para justificar outras escolhas (parse manual de query
string), e uma exceção para desenvolvimento o enfraqueceria.

**Alternatives considered**:
- **Deixar a página de exceção de desenvolvimento ativa em `Development` e só usar o handler
  global em outros ambientes**: é o padrão mais comum em templates ASP.NET Core, mas rejeitada
  aqui por violar FR-014 como está escrito e por ser exatamente o comportamento que expôs o
  defeito original (stack trace vazando porque o `docker-compose.yml` usa
  `ASPNETCORE_ENVIRONMENT=Development` por padrão).
- **Middleware customizado (`app.Use(async (context, next) => ...)`) em vez de
  `IExceptionHandler`**: equivalente em efeito, mas `IExceptionHandler` é a API padrão do
  framework para este propósito desde o .NET 8 e integra nativamente com `ProblemDetails` — sem
  motivo para reimplementar o mecanismo de captura.

## 7. Remoção de scaffolding e deduplicação: sem mudança de comportamento

**Decision**: `PingQuery`/`PingQueryHandler` e `SampleSource`/`SampleDestination`/
`SampleMappingConfig` são removidos inteiramente. `MediatorRegistrationTests` passa a despachar
`GetSaleQuery` (ou outra Query real já existente) via `IMediator`, confirmando o registro do
MediatR sem depender de um tipo de exemplo. `MapsterConfigurationTests` passa a exercer um
`TypeAdapterConfig` real do domínio (por exemplo, `ExternalReferenceRequest → ExternalReference`,
já registrado por `CreateSaleMappingConfig`). `SaleItem.ValidateChange(int quantity, decimal
unitPrice)` é extraído como método estático, reaproveitando exatamente as mesmas mensagens hoje
duplicadas entre `SaleItem.Create` e `Sale.ReconcileExistingItem`; ambos os pontos passam a
chamá-lo. `ResultExtensions.ToHttpResult(this Result result, params string[] notFoundKeys)` em
`SalesApi.Api.Common` centraliza a tradução `Result` → `IResult`, reaproveitada pelos 6 endpoints
de `SalesEndpoints` — cada endpoint passa a declarar apenas quais chaves de erro significam
"não encontrado" (`"id"`, `"itemId"`, conforme o caso), eliminando a repetição do bloco de
seleção de `errors` e da decisão 400/404.

**Rationale**: nenhuma dessas mudanças altera contrato observável — é puramente uma correção de
SRP/DRY sobre código já existente, coerente com o padrão que o próprio projeto já demonstra
(commit `0bb353d`, citado no Guia Técnico do Notion, é exatamente uma refatoração motivada por
apontamento de qualidade sem mudança de comportamento).

**Alternatives considered**:
- **Manter `PingQuery`/`SampleMapping` e apenas adicionar testes novos ao lado**: rejeitada —
  não resolve o ruído que a análise identificou (tipos de exemplo sem relação com o domínio de
  vendas, sinal de descuido para quem revisa).
- **Deduplicar a validação de item extraindo para uma classe de validação separada em vez de um
  método estático em `SaleItem`**: rejeitada por complexidade desnecessária — a regra pertence
  naturalmente ao próprio `SaleItem` (mesmo padrão já usado pelo `Create` atual), sem justificar
  uma abstração nova.

## 8. Documentação: portar o conteúdo do Notion, não reescrever do zero

**Decision**: o `README.md` reescrito reaproveita a estrutura e o conteúdo já existentes nas
páginas do Notion (Propósito, Escopo, Arquitetura, Superfície da API, política de desconto,
decisões de desenho) — adaptado ao formato de README (mais direto, com comandos executáveis) — e
acrescenta duas seções novas: o fluxo de provisionamento via `migrator` (US1) e uma nota sobre o
caminho de migration recomendado para um ambiente produtivo real (script idempotente revisado
manualmente, ver seção 1 deste documento), deixando explícito que o `migrator` do
`docker-compose.yml` é a escolha certa para o ambiente de desenvolvimento/avaliação, não uma
recomendação de produção.

**Rationale**: o conteúdo do Notion já foi escrito, revisado e é preciso — portar evita
divergência futura entre as duas fontes e resolve diretamente FR-030 a FR-033 sem exigir nova
redação de zero.

**Alternatives considered**: nenhuma — reescrever do zero sem aproveitar o Notion arriscaria
reintroduzir a mesma divergência doc↔código que esta feature existe para eliminar.
