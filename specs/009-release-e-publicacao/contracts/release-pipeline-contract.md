# Contrato: Pipeline de Release

**Feature**: `009-release-e-publicacao`

Contrato observável do fluxo de release — quem dispara o quê, em que ordem, e o que precisa ser
verdade para que uma publicação seja considerada concluída. Cobre FR-005, FR-006, FR-016 a
FR-024.

## Fluxo, em ordem

Todo o fluxo abaixo roda como jobs sequenciais (via `needs`) de um único workflow,
`.github/workflows/ci-cd.yml` — não como workflows separados reagindo ao mesmo push (decisão
original revertida após um incidente em produção onde o CD publicou antes do CI terminar; ver
`research.md`, seção 9). Cada estágio só inicia depois que o anterior concluir com sucesso, **na
mesma execução**:

```
push em main (commit convencional)
        │
        ▼
build → test → sonar
        │
        ▼
job release-please mantém/atualiza PR de release
   (version bump + CHANGELOG.md, revisável)
        │
        ▼  (merge humano do PR de release → novo push em main)
build → test → sonar (de novo, para o commit de merge)
        │
        ▼
job release-please cria tag Git + GitHub Release NESTA execução
   (release_created=true)
        │
        ▼  (needs: release-please, if: release_created == 'true')
job publish dispara
        │
        ├─ build + push imagem da aplicação  (tags: X.Y.Z, latest)
        ├─ build + push imagem do migrator   (tags: X.Y.Z, latest)
        │
        ▼
smoke test: migrator publicado roda contra Postgres efêmero do job
        │
   ┌────┴────┐
 sucesso   falha
   │           │
   ▼           ▼
release      job falha; imagens já publicadas nesta execução
permanece    permanecem publicadas com o defeito registrado no
válida       log do job — não há rollback automático (ver Edge Cases)
```

## Garantias

- **Gatilho único**: o job `publish` só roda quando o job `release-please` da mesma execução
  produziu `release_created == 'true'` — ou seja, quando o push que acabou de passar por
  `build`/`test`/`sonar` era o merge do PR de release. Nenhum push direto em `main` fora desse
  caminho, nem tag criada manualmente fora do fluxo release-please, dispara publicação. Antes,
  esse gatilho era o evento `release: published` de um workflow `cd.yml` separado — revisado por
  não garantir ordem alguma em relação à execução do CI sobre o mesmo commit (ver `research.md`,
  seção 9).
- **Atomicidade por par**: as duas imagens (aplicação + migrator) de uma release são publicadas
  na mesma execução do workflow. Se o build ou push de qualquer uma delas falhar, o job falha
  antes do smoke test — nenhuma "meia publicação" é reportada como bem-sucedida pelo workflow.
- **Gate de verificação**: o smoke test roda depois do push de ambas as imagens e antes de o job
  ser marcado como bem-sucedido. Falha no smoke test = job `publish` falho, visível no GitHub
  Actions, ainda que — por limitação do Docker Hub, que não suporta despublicação atômica via
  Actions padrão — as tags já enviadas permaneçam no registro (mitigação: FR-004 garante que
  `latest` só deveria ser promovida por quem observa o job verde; a versão exata fica publicada
  mas identificável como não verificada pelo histórico do Actions).
- **Changelog é pré-requisito, não pós-requisito**: o `CHANGELOG.md` e a tag já existem *antes*
  do job `publish` rodar — são o próprio gatilho, não uma consequência da publicação de imagem.

## Segredos exigidos pelo workflow

| Nome | Tipo | Escopo |
|---|---|---|
| `DOCKERHUB_USERNAME` | Variável de repositório/ambiente | Login no Docker Hub |
| `DOCKERHUB_TOKEN` | Secret de repositório/ambiente | Access token do Docker Hub (não a senha) |

Ambos configurados manualmente no GitHub antes da primeira execução — fora do escopo de
automação desta feature (ver Assumptions da spec).

## O que este contrato não garante

- Rollback automático de uma imagem publicada com defeito não capturado pelo smoke test.
- Retenção ou expurgo de versões antigas no Docker Hub (fora de escopo).
- Implantação da imagem publicada em qualquer ambiente produtivo real — o contrato termina na
  disponibilização do artefato verificado (entrega contínua, não deploy contínuo).
