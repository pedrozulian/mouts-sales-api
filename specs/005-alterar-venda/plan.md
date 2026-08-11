# Implementation Plan: Alterar Venda

**Branch**: `005-alterar-venda` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-alterar-venda/spec.md`

## Summary

Alterar venda (`PUT /api/sales/{id}`): substitui o cabeçalho de uma venda ativa (cliente,
filial e data — todos obrigatórios, FR-002) e reconcilia seus itens conforme o corpo da
requisição — item com `id` conhecido é atualizado (quantidade e/ou preço, nunca produto,
FR-004), item sem `id` é adicionado, item ausente do corpo é cancelado logicamente, nunca
removido. Abordagem: um novo método de escrita no agregado `Sale` (`Sale.Update`), seguindo o
mesmo padrão *two-pass* (validar tudo primeiro, só então mutar) já usado em `Sale.Create` —
quando toda a reconciliação é válida, o agregado atualiza o cabeçalho, aplica as mudanças de
item, cancela logicamente os itens implicitamente removidos, recalcula desconto/total por item
e o total geral, e registra um `ItemCancelled` por item cancelado seguido de um único
`SaleModified`. Um novo Command MediatR (`UpdateSaleCommand`) na camada Application carrega a
venda **com tracking** (`Include(Items)`, sem `AsNoTracking` — diferente da leitura pura de
`003-consultar-venda`), delega toda a regra de negócio ao agregado e usa a mesma convenção já
estabelecida por `GetSaleQueryHandler` (chave de erro `"id"`) para o endpoint distinguir "venda
não encontrada" (`404`) de violação de regra de negócio (`400`). Nenhuma tabela ou coluna nova é
necessária — a reconciliação só grava nas colunas já existentes de `sales`/`sale_items`
(nenhuma migration EF Core nesta feature).

## Technical Context

**Language/Version**: C# 12, .NET 8.0 (LTS)

**Primary Dependencies**: MediatR 14.2.0 (novo `UpdateSaleCommand` + handler, dois novos
`INotificationHandler` para `SaleModified` e `ItemCancelled`), Mapster 10.0.11 (novo
`UpdateSaleMappingConfig`: `UpdateSaleRequest → UpdateSaleCommand`, `SaleItemChangeRequest →
SaleItemChangeInput`), ASP.NET Core Minimal APIs (novo `MapPut("/api/sales/{id:guid}", ...)` em
`SalesEndpoints`), Entity Framework Core 8 via `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.11
(leitura **com tracking** via `IApplicationDbContext.Sales.Include(s => s.Items)`),
Serilog.AspNetCore (log estruturado nos novos event handlers e no handler do comando). Nenhuma
dependência nova é adicionada aos `.csproj` existentes.

**Storage**: PostgreSQL 16. Reaproveita integralmente as tabelas `sales`/`sale_items` já
criadas pela migration `CreateSales` (`002-registrar-venda`) — `is_cancelled`, `updated_at`,
`discount_percentage`, `discount_amount` e `total_amount` de `sale_items` já existem e cobrem
exatamente o que a reconciliação precisa gravar. O índice único `uq_sale_product` (sale_id +
product_id) já reforça a INV-03 também no banco. Nenhuma migration nova nesta feature.

**Testing**: xUnit. `SalesApi.Domain.Tests/Sales/SaleTests.cs` recebe os novos casos de
`Sale.Update` (reconciliação — atualizar/adicionar/cancelar implícito —, cada invariante
INV-01 a INV-04, INV-06, INV-07, e a regra de imutabilidade de produto e de item já cancelado
definida na sessão de clarificação). `SalesApi.Application.Tests/Sales/` recebe
`UpdateSaleCommandHandlerTests` (fluxo principal, venda não encontrada, cada caminho de
rejeição) e testes para os dois novos event handlers. `SalesApi.Api.Tests/Sales/` recebe
`UpdateSaleEndpointTests` via `WebApplicationFactory<Program>`, cobrindo `200`/`400`/`404`
ponta a ponta (Postgres local, mesmo padrão de `AppDbContextConnectionTests`).

**Target Platform**: serviço web ASP.NET Core containerizado (Linux, Docker), execução local
via `docker compose` ou `dotnet run` — mesmo ambiente já orquestrado pela feature 002.

**Project Type**: web-service — solução única em Clean Architecture (4 projetos já
existentes: Domain, Application, Infrastructure, Api). Esta feature é a primeira, depois do
registro (002), a acrescentar um método de escrita ao agregado `Sale`: toca Domain,
Application e Api. Infrastructure não é alterada.

**Performance Goals**: sem meta formal de throughput — protótipo de avaliação técnica com uso
local/single-instance. A reconciliação opera sobre uma única venda por requisição (sem
varredura de tabela), usando o `ChangeTracker` do EF Core já carregado via `Include`.

**Constraints**: cobertura mínima de 90% (Princípio IX); venda cancelada é estado imutável —
nenhuma alteração de cabeçalho ou item é aceita (INV-07/FR-008); produto de um item existente é
imutável — trocar de produto exige cancelar e adicionar (FR-004, decisão da sessão de
clarificação); campo de data é obrigatório no corpo do PUT, diferente do registro (FR-002,
idem); `id` que não pertence à venda **ou** que referencia um item já cancelado é rejeitado
com `400`, sem suporte a reativação (FR-010, idem); desconto e totais nunca são aceitos do
cliente, sempre recalculados (FR-014); toda resposta de erro segue o mesmo contrato
`{ "errors": [...] }` já usado pelos demais casos de uso.

**Scale/Scope**: um único endpoint (`PUT /api/sales/{id}`); volume de dados de teste, sem carga
concorrente real (concorrência entre alterações simultâneas na mesma venda está fora de escopo
— ver `spec.md`, seção Assumptions).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como esta feature atende | Status |
|---|---|---|
| I. DDD | Toda a lógica de reconciliação (o que é atualização, adição ou cancelamento implícito de item; recálculo de desconto e total; cada invariante) vive em `Sale.Update`/`SaleItem`, nunca no handler ou no endpoint. | PASS |
| II. TDD | Tasks (a gerar via `/speckit-tasks`) seguem Red-Green-Refactor: testes de `Sale.Update` para cada cenário de reconciliação e cada invariante são escritos antes da implementação do método. | PASS |
| III. SOLID | `UpdateSaleCommandHandler` com responsabilidade única (carregar a venda, delegar ao domínio, persistir); `IApplicationDbContext` injetado por interface via construtor; a reconciliação fica centralizada em `Sale.Update` em vez de duplicada entre handler e endpoint (SRP). | PASS |
| IV. Português | Este plano, `research.md`, `data-model.md`, `quickstart.md` e o contrato estão em português; identificadores de código permanecem em inglês. | PASS |
| V. Clean Architecture | Código novo/ajustado em Domain (`Sale`, `SaleItem`, dois novos eventos), Application (`Sales/Update/`, dois novos event handlers) e Api (extensão de `SalesEndpoints`); Infrastructure não é tocada — nenhuma migration, nenhuma configuração nova. | PASS |
| VI. Eventos via Mediator | `ItemCancelled` (um por item removido implicitamente) e `SaleModified` (um único, sempre) são acumulados no agregado via `AddDomainEvent` e despachados pelo `AppDbContext.SaveChangesAsync` já genérico — nenhuma alteração de infraestrutura necessária. Handlers de log ficam na Application, desacoplados entre si. | PASS |
| VII. Result/Notification | Toda violação (venda cancelada, corpo sem item, `id` de item inexistente ou já cancelado, produto alterado, quantidade fora do intervalo, produto duplicado, data ausente) é comunicada via `Result<Sale>.Failure(Notification[])`, nunca exception. O endpoint distingue `404` de `400` pela chave `"id"` do erro — mesma convenção já usada por `GetSaleQueryHandler` (ver `research.md`, seção 2). | PASS |
| VIII. Observabilidade | Logging estruturado explícito no `UpdateSaleCommandHandler` (venda não encontrada) e nos novos `INotificationHandler<SaleModified>`/`INotificationHandler<ItemCancelled>`, no mesmo formato de `SaleCreatedEventHandler`. | PASS |
| IX. Qualidade de Código | Tasks incluem testes para reconciliação, cada invariante e os três caminhos de resposta (`200`/`400`/`404`) necessários para manter o gate de 90% de cobertura verificado no CI (SonarCloud). | PASS |
| X. Docker | Nenhum novo recurso de infraestrutura — a feature roda inteiramente sobre o PostgreSQL já orquestrado pelo `docker-compose.yml` existente, sem migration nova. | PASS |

Nenhuma violação identificada. `Complexity Tracking` não se aplica.

**Reavaliação pós Fase 1**: `data-model.md` acrescenta um método de agregado (`Sale.Update`),
dois métodos de entidade filha (`SaleItem.ApplyChange`, `SaleItem.Cancel`) e dois eventos de
domínio (`SaleModified`, `ItemCancelled`) — todos já previstos na documentação DDD do Notion
(diagrama de classes e seção "Eventos de domínio"), sem introduzir nenhuma tabela, coluna ou
conceito fora do que o domínio já modelava. `contracts/update-sale.md` não introduz nenhum
campo de resposta fora do já usado por `SaleResponse`/`SaleItemResponse` (`003-consultar-venda`).
Gate permanece PASS.

## Project Structure

### Documentation (this feature)

```text
specs/005-alterar-venda/
├── plan.md                    # Este arquivo (/speckit-plan)
├── research.md                # Fase 0 (/speckit-plan)
├── data-model.md               # Fase 1 (/speckit-plan)
├── quickstart.md               # Fase 1 (/speckit-plan)
├── contracts/
│   └── update-sale.md         # Fase 1 (/speckit-plan)
├── checklists/
│   └── requirements.md
└── tasks.md                   # Fase 2 (/speckit-tasks — ainda não gerado)
```

### Source Code (repository root)

```text
src/
├── SalesApi.Domain/
│   └── Sales/
│       ├── Sale.cs                        # AJUSTAR — novo método público Update(customer, branch, saleDate, items)
│       ├── SaleItem.cs                    # AJUSTAR — novos métodos ApplyChange(quantity, unitPrice) e Cancel()
│       ├── SaleItemChangeInput.cs         # NOVO — record (Id?, Product, Quantity, UnitPrice)
│       └── Events/
│           ├── SaleCreated.cs             # já existe
│           ├── SaleModified.cs            # NOVO — SaleId, SaleNumber, TotalAmount, OccurredOn
│           └── ItemCancelled.cs           # NOVO — SaleId, SaleItemId, ProductId, Quantity, OccurredOn
│
├── SalesApi.Application/
│   └── Sales/
│       ├── Create/                        # já existe
│       ├── Get/                           # já existe
│       ├── List/                          # já existe
│       ├── Dtos/
│       │   ├── SaleItemChangeRequest.cs   # NOVO — (Id?, Product, Quantity, UnitPrice)
│       │   └── UpdateSaleRequest.cs       # NOVO — (SaleDate?, Customer, Branch, Items)
│       ├── Events/
│       │   ├── SaleCreatedEventHandler.cs     # já existe
│       │   ├── SaleModifiedEventHandler.cs    # NOVO — log estruturado
│       │   └── ItemCancelledEventHandler.cs   # NOVO — log estruturado
│       └── Update/                        # NOVO
│           ├── UpdateSaleCommand.cs           # IRequest<Result<SaleResponse>>; Id vem da rota
│           ├── UpdateSaleCommandHandler.cs    # carrega com tracking, delega a Sale.Update, salva
│           └── UpdateSaleMappingConfig.cs     # UpdateSaleRequest -> UpdateSaleCommand, item -> SaleItemChangeInput
│
├── SalesApi.Infrastructure/               # SEM ALTERAÇÃO
│
└── SalesApi.Api/
    └── Sales/
        └── SalesEndpoints.cs              # AJUSTAR — adicionar MapPut("/api/sales/{id:guid}", UpdateSale)

tests/
├── SalesApi.Domain.Tests/Sales/
│   └── SaleTests.cs                       # AJUSTAR — casos de Update: atualizar/adicionar/cancelar implícito, cada invariante
├── SalesApi.Application.Tests/Sales/
│   ├── UpdateSaleCommandHandlerTests.cs   # NOVO — fluxo principal, venda não encontrada, cada rejeição
│   └── Events/
│       ├── SaleModifiedEventHandlerTests.cs   # NOVO
│       └── ItemCancelledEventHandlerTests.cs  # NOVO
└── SalesApi.Api.Tests/Sales/
    └── UpdateSaleEndpointTests.cs         # NOVO — WebApplicationFactory, 200/400/404, Postgres local
```

**Structure Decision**: mantém a solução única em Clean Architecture já estabelecida (4
projetos), sem criar nenhum projeto novo. Diferente de `003-consultar-venda` e
`004-listar-vendas` — que só tocaram Application/Api (e, no caso de 004, um índice em
Infrastructure) —, esta é a primeira feature depois do registro (002) a estender o próprio
agregado `Sale` com um segundo método de escrita, porque a reconciliação de itens (atualizar,
adicionar, cancelar implicitamente) é regra de negócio e, pelo Princípio I, não pode viver no
handler. Infrastructure permanece intocada porque a reconciliação grava exclusivamente em
colunas já existentes — nenhuma migration é necessária. O código novo de Application segue o
mesmo padrão de pastas por caso de uso dentro de `Sales/` (`Update/`, ao lado de `Create/`,
`Get/` e `List/`), e os dois novos event handlers ficam em `Sales/Events/`, ao lado de
`SaleCreatedEventHandler`.

## Complexity Tracking

*Sem violações — seção não se aplica a este plano.*
