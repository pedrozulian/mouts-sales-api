# Data Model: Configuração Inicial do Projeto

Esta spec cobre exclusivamente a fundação técnica do projeto. Conforme a seção **Assumptions**
de [spec.md](./spec.md), nenhuma entidade de negócio (ex.: Venda, Produto, Cliente) é
introduzida nesta fase — isso é responsabilidade de specs futuras de funcionalidade.

## Escopo de dados nesta fase

- Não há tabelas de negócio a criar nem migrations de domínio a gerar.
- O `DbContext` da camada Infrastructure existe apenas para provar a conectividade com o
  PostgreSQL (consumido pelo health check descrito em
  [contracts/health-check.md](./contracts/health-check.md)); ele não expõe nenhum `DbSet` de
  negócio.

## Blocos de construção reservados para specs futuras

Para que as próximas specs (que vão introduzir entidades reais) já encontrem a fundação
correta, esta fase cria apenas os seguintes tipos-base na camada Domain, sem estado ou
comportamento de negócio associado:

- **Entity (base)**: classe-base abstrata para entidades de domínio, provendo identidade
  (Id) e comparação por identidade. Sem campos de negócio.
- **DomainEvent (base)**: contrato-base para eventos de domínio disparados via MediatR
  (Princípio VI da constitution). Sem eventos concretos definidos ainda.
- **Result / Notification (base)**: tipo-base para o padrão de retorno de erros de
  validação/negócio (Princípio VII da constitution), usado no lugar de exceptions para
  fluxos de erro esperados. Sem regras de negócio associadas ainda.

Nenhum desses tipos-base é uma "entidade de dados" no sentido tradicional — são contratos de
programação que as specs de funcionalidade (CRUD de vendas) vão implementar e estender.
