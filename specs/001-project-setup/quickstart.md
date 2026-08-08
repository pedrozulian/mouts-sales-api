# Quickstart: Configuração Inicial do Projeto

Guia para validar, de ponta a ponta, que a fundação técnica descrita em
[spec.md](./spec.md) está funcional. Cobre as User Stories 1, 2, 3 e 4.

## Pré-requisitos

- Docker e Docker Compose instalados e em execução.
- .NET 8 SDK instalado (necessário apenas para rodar comandos `dotnet` fora do container,
  ex.: durante o desenvolvimento local).

## 1. Subir o ambiente completo (US1)

```bash
docker compose -f docker/docker-compose.yml up -d
```

**Resultado esperado**: três containers saudáveis — Api, PostgreSQL e SonarQube (local) —
sem nenhum passo manual adicional (FR-009, SC-002).

## 2. Validar a fundação de código em camadas (US2)

```bash
dotnet build SalesApi.sln
```

**Resultado esperado**: build bem-sucedido, sem erros, confirmando que as quatro camadas
(Domain, Application, Infrastructure, Api) compilam e respeitam a direção de dependência
definida no [plan.md](./plan.md) (FR-001).

## 3. Rodar a suíte de testes (US3)

```bash
dotnet test SalesApi.sln --collect:"XPlat Code Coverage"
```

**Resultado esperado**: todos os testes passam, incluindo o smoke test inicial, mesmo sem
nenhuma funcionalidade de negócio implementada (FR-002, SC-003).

## 4. Explorar a API e verificar a saúde da aplicação (US4)

Com o ambiente no ar (passo 1):

- Abrir `http://localhost:<porta-api>/swagger` no navegador → a documentação interativa
  carrega e lista os endpoints disponíveis (FR-003).
- Consultar `http://localhost:<porta-api>/health` → resposta `200 OK` confirmando que a
  aplicação e o PostgreSQL estão saudáveis (contrato completo em
  [contracts/health-check.md](./contracts/health-check.md), FR-004, SC-005).

## 5. Validar a análise de qualidade local (Princípio IX da constitution)

Com o SonarQube local no ar (passo 1), rodar a análise apontando para a instância local
(consulte o README do repositório para o comando exato de scanner usado localmente) e
confirmar que o relatório de cobertura é processado (FR-012).

## 6. Validar o pipeline de CI (US5)

Abrir um Pull Request no GitHub e observar, na aba de Checks:

1. **build** — compila a solução.
2. **test** — executa a suíte de testes e gera o relatório de cobertura.
3. **sonar** — envia a análise ao SonarCloud e aplica o gate de qualidade (bloqueia o merge
   se a cobertura ficar abaixo de 90% ou houver falha crítica de qualidade — FR-010, FR-011).

**Resultado esperado**: pipeline completo (build → test → sonar) concluído em até 10 minutos
(SC-006).

## 7. Onboarding via README (US6)

Como validação final, seguir apenas o `README.md` do repositório, do zero (sem consultar
este quickstart), e confirmar que os passos 1 a 4 acima são alcançáveis apenas com o que
está documentado ali (FR-013, SC-001).
