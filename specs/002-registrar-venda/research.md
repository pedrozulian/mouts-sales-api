# Research: Registrar Venda

Nenhum `NEEDS CLARIFICATION` restou no Technical Context do `plan.md` — a documentação DDD
(Notion) e a constitution já fixam a maior parte das escolhas. Este documento registra as
decisões técnicas específicas desta feature que não estavam determinadas de antemão.

## 1. Despacho dos eventos de domínio

**Decision**: `Entity` (`SalesApi.Domain.Common`) ganha uma coleção interna de
`DomainEvent`, com métodos protegidos para o agregado registrar e limpar eventos.
`AppDbContext.SaveChangesAsync` é sobrescrito: chama `base.SaveChangesAsync` e, somente em
caso de sucesso, coleta os eventos acumulados nas entidades rastreadas e os publica via
`IPublisher` (MediatR) antes de retornar.

**Rationale**: garante que o evento só saia após o commit (FR-015 e User Story 3: nenhum
evento em caso de falha), sem introduzir um Outbox Pattern — complexidade desnecessária para
um protótipo sem broker real.

**Alternatives considered**:
- **Outbox Pattern** (tabela de eventos pendentes + processo de envio): mais robusto para
  garantir entrega em sistemas distribuídos reais, mas desproporcional ao escopo (o evento
  vira apenas uma linha de log).
- **Publicar dentro do próprio handler do comando**, antes do `SaveChanges`: rejeitado porque
  o evento sairia mesmo se a persistência falhasse na sequência.

## 2. Geração do número da venda (SaleNumber)

**Decision**: sequence do PostgreSQL (`sale_number_seq`), lida pela Infrastructure e
formatada como string legível (ex.: `V-000123`), atribuída dentro de `Sale.Create`.

**Rationale**: garante unicidade (INV-10, SC-003) mesmo sob concorrência, sem exigir lock
otimista customizado — é a garantia nativa do banco.

**Alternatives considered**:
- **GUID como número de negócio**: unicidade trivial, mas não é "legível por humanos" como
  pede FR-009 e a documentação de domínio já publicada.
- **Contador em memória na aplicação**: sujeito a condição de corrida entre instâncias;
  rejeitado.

## 3. Validação de entrada

**Decision**: validação em duas camadas, ambas via `Result`/`Notification` (Princípio VII),
sem biblioteca de validação externa (não faz parte da Stack Tecnológica Obrigatória):
validação de forma (payload ausente/vazio) no handler da Application; invariantes de negócio
(INV-01 a INV-05) dentro de `Sale.Create`/`SaleItem`, no Domain.

**Rationale**: mantém a regra "nenhuma regra de negócio fora do Domain" (Princípio I) e evita
adicionar uma dependência não prevista na constitution.

**Alternatives considered**:
- **FluentValidation**: comum no ecossistema .NET, mas exigiria amendment da constitution
  para entrar na Stack Tecnológica Obrigatória; rejeitado para não expandir escopo além do
  necessário para este protótipo.

## 4. Estilo do endpoint (Minimal API vs. Controllers)

**Decision**: Minimal API, com um extension method `MapSalesEndpoints(this
IEndpointRouteBuilder)` chamado a partir de `Program.cs`.

**Rationale**: consistente com o único endpoint hoje existente (`/health`, mapeado
diretamente em `Program.cs`); evita introduzir dois estilos de roteamento no mesmo projeto
pequeno.

**Alternatives considered**:
- **Controllers (MVC)**: mais convencional em testes técnicos semelhantes de mercado, mas
  divergiria do padrão já estabelecido no repositório sem necessidade real.

## 5. Mapeamento de persistência do External Identity

**Decision**: `ExternalReference` (`record` com `Id` e `Name`) mapeado como *owned type* do
EF Core, sem tabela própria, reutilizado em `Sale.Customer`, `Sale.Branch` e
`SaleItem.Product` — conforme já especificado na documentação DDD (Notion, página "Bounded
Contexts").

**Rationale**: nenhuma foreign key real deve existir para essas colunas, já que apontariam
para agregados de outros bounded contexts que não existem neste banco.

**Alternatives considered**: nenhuma — decisão já validada na documentação de domínio antes
desta spec.

## 6. Ambiente de teste de integração

**Decision**: o teste de integração do endpoint (`CreateSaleEndpointTests`) usa
`WebApplicationFactory<Program>` contra o PostgreSQL local do `docker-compose.yml`, seguindo
o mesmo padrão já usado por `AppDbContextConnectionTests` (variável
`ConnectionStrings__DefaultConnection`, com fallback para o banco local).

**Rationale**: mantém um único padrão de teste de integração no repositório; introduzir
Testcontainers agora seria uma mudança de stack não coberta pela constitution.

**Alternatives considered**:
- **Testcontainers**: isola melhor os testes de integração, mas é uma dependência nova fora
  da Stack Tecnológica Obrigatória; fica como possível evolução futura, fora do escopo desta
  feature.

**Output**: todas as decisões técnicas necessárias para o desenho estão resolvidas; nenhum
`NEEDS CLARIFICATION` restante.
