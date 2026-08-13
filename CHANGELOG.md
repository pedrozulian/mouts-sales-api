# Changelog

## [1.2.1](https://github.com/pedrozulian/mouts-sales-api/compare/v1.2.0...v1.2.1) (2026-08-13)


### Bug Fixes

* consolida ci, release-please e cd em um único workflow sequencial ([a97034a](https://github.com/pedrozulian/mouts-sales-api/commit/a97034a37543f3c4ecf47518d31b270add660097))
* declara permissões de leitura no nível de job em vez de workflow ([fa2eefe](https://github.com/pedrozulian/mouts-sales-api/commit/fa2eefeb268c648bb64677c13edf294e33a54251))
* elimina a corrida entre CI e CD no pipeline de release ([3e2ceea](https://github.com/pedrozulian/mouts-sales-api/commit/3e2ceeae2c8b6812e5fcee3093949e22e0bb1d0b))

## [1.2.0](https://github.com/pedrozulian/mouts-sales-api/compare/v1.1.0...v1.2.0) (2026-08-13)


### Features

* habilita auto-merge do pr de release condicionado aos checks obrigatórios ([f961ae8](https://github.com/pedrozulian/mouts-sales-api/commit/f961ae8caf6cc40cf3924f6e2d8bdd6faf9cb361))

## [1.1.0](https://github.com/pedrozulian/mouts-sales-api/compare/v1.0.0...v1.1.0) (2026-08-13)


### Features

* adiciona a alteração de venda ao agregado Sale ([c52218c](https://github.com/pedrozulian/mouts-sales-api/commit/c52218c402b49bd9e55969bb60d9d80600364854))
* adiciona a consulta de venda na camada de aplicação ([84815d4](https://github.com/pedrozulian/mouts-sales-api/commit/84815d4c00d4d876fa326d1d446a3a620632ca7b))
* adiciona a listagem de vendas na camada de aplicação ([f77e9f3](https://github.com/pedrozulian/mouts-sales-api/commit/f77e9f3eaa8ce6cdc58bc8af1a29c4d8383498f8))
* adiciona ambiente Docker com Api, PostgreSQL e SonarQube (001-project-setup) ([568b714](https://github.com/pedrozulian/mouts-sales-api/commit/568b71472d638e57e7916ee090d6353c7c35d11d))
* adiciona health check de migrations pendentes do PostgreSQL ([048becd](https://github.com/pedrozulian/mouts-sales-api/commit/048becd765277b6300e985dbf10b3ab81af1fcc7))
* adiciona índices de consulta em customer_id, branch_id e sale_date ([b556bad](https://github.com/pedrozulian/mouts-sales-api/commit/b556bada1d1e70c3fda7812d96d66579e803d07e))
* adiciona o cancelamento de item ao agregado Sale ([79a561f](https://github.com/pedrozulian/mouts-sales-api/commit/79a561f656d8dbfa8639fd3f4c303f7dd101b2a7))
* adiciona o cancelamento de venda ao agregado Sale ([5f7bc8f](https://github.com/pedrozulian/mouts-sales-api/commit/5f7bc8fce56597f54b3f7b6284ec98c933b55bd7))
* adiciona o comando de alteração de venda na camada de aplicação ([0c3acdf](https://github.com/pedrozulian/mouts-sales-api/commit/0c3acdf7d7fc7c91027a32ec97b010aa79538db8))
* adiciona o comando de cancelamento de item na camada de aplicação ([f266b43](https://github.com/pedrozulian/mouts-sales-api/commit/f266b43a3cff4a1264922bc001e38c5765acd5a6))
* adiciona o comando de cancelamento de venda na camada de aplicação ([481861d](https://github.com/pedrozulian/mouts-sales-api/commit/481861d8fe65cb5c0c3a8657601834a0ed58d173))
* adiciona o comando de registro de venda na camada de aplicação ([7d1ff84](https://github.com/pedrozulian/mouts-sales-api/commit/7d1ff84393ae00df44e1e1477df45f3224577625))
* adiciona suporte a eventos de domínio na entidade base ([3ec0cb9](https://github.com/pedrozulian/mouts-sales-api/commit/3ec0cb9643b5923c22dd12c339deca113722c6ff))
* adiciona tratamento global de exceções não tratadas ([dd707b2](https://github.com/pedrozulian/mouts-sales-api/commit/dd707b20a42c0bca51ad25725ccd60bcdc8284ec))
* adiciona versionamento semântico e changelog automatizados via release-please ([e6b2718](https://github.com/pedrozulian/mouts-sales-api/commit/e6b2718c15bcff2949e4d4cce232c85659e8bc08))
* arredonda desconto e total do item de venda em duas casas decimais ([d7c3eaf](https://github.com/pedrozulian/mouts-sales-api/commit/d7c3eafe857b74539c58fe048e9ee8e88b531152))
* compõe a Api com Swagger, health check e logging estruturado (001-project-setup) ([13fcf46](https://github.com/pedrozulian/mouts-sales-api/commit/13fcf46e08a830781ce3f92983ef8c4c2225e381))
* cria projetos em camadas e projetos de teste (001-project-setup) ([edc4358](https://github.com/pedrozulian/mouts-sales-api/commit/edc43588c15b804406a55202cd1386285ca4cdd3))
* expõe o endpoint DELETE /api/sales/{id} ([a106ec2](https://github.com/pedrozulian/mouts-sales-api/commit/a106ec28c2da3ac97548e001ee6965f172428dbc))
* expõe o endpoint DELETE /api/sales/{id}/items/{itemId} ([5e32258](https://github.com/pedrozulian/mouts-sales-api/commit/5e32258a7d0ccbe451871c2b8fd8c1c8622c75d5))
* expõe o endpoint GET /api/sales ([5886eeb](https://github.com/pedrozulian/mouts-sales-api/commit/5886eebb3b750792a468b4cd74dd3eff26cfdfde))
* expõe o endpoint GET /api/sales/{id} ([5027853](https://github.com/pedrozulian/mouts-sales-api/commit/50278539b1f0e3a53897a6ed73a75a8dd63144df))
* expõe o endpoint POST /api/sales ([d743582](https://github.com/pedrozulian/mouts-sales-api/commit/d743582e8024d9094f38418887c5d09a5bd19112))
* expõe o endpoint PUT /api/sales/{id} ([cedb151](https://github.com/pedrozulian/mouts-sales-api/commit/cedb15114c4ce2275a8124fef2872cc3caaee463))
* garante cancelamento consistente sob duas requisições concorrentes ([ade9fbc](https://github.com/pedrozulian/mouts-sales-api/commit/ade9fbc60f58f5c205ff22fb5b17881e694b4ae0))
* implementa fundação de domínio e arquitetura (001-project-setup) ([34151d9](https://github.com/pedrozulian/mouts-sales-api/commit/34151d9b672b8f1ade0b1613081bfcfbdc0aeb0b))
* implementa o agregado Sale com desconto por quantidade e invariantes de negócio ([716492a](https://github.com/pedrozulian/mouts-sales-api/commit/716492a7aeffed158e5e8a4c490382b8fd03aa5a))
* inclui número da venda no evento de cancelamento de item ([665e95d](https://github.com/pedrozulian/mouts-sales-api/commit/665e95d0d211b4af4df9def09118cbcb8da28502))
* persiste vendas via EF Core e publica eventos de domínio após o commit ([aa72bec](https://github.com/pedrozulian/mouts-sales-api/commit/aa72bec85ff749470dc089833289a4c3560ab5cc))
* provisiona o schema do banco via serviço migrator no ambiente Docker ([d371493](https://github.com/pedrozulian/mouts-sales-api/commit/d3714939e1f30b6d6dd22acc72132c59db432173))
* publica as imagens da api e do migrator no docker hub via workflow de cd ([34721cb](https://github.com/pedrozulian/mouts-sales-api/commit/34721cb77e6cd16c2ad6538fd52ea9138d8ea449))
* rejeita reintrodução de produto de item cancelado ao alterar venda ([4899d58](https://github.com/pedrozulian/mouts-sales-api/commit/4899d5848c3c2e544910bb3cd6391c2c65e395c3))
* valida connection string obrigatória e fixa ambiente production por padrão ([a8a15e7](https://github.com/pedrozulian/mouts-sales-api/commit/a8a15e76f343815922669d3855f33449f67409a6))
* verifica o migrator publicado com smoke test antes de concluir a release ([9a7b271](https://github.com/pedrozulian/mouts-sales-api/commit/9a7b2712b5d27f4ec41eb5f3edc34805b0bbec17))


### Bug Fixes

* adiciona referência ao EF Core no projeto de Application ([21fb88c](https://github.com/pedrozulian/mouts-sales-api/commit/21fb88c392a01db81360059c108346713c7b95ec))
* aplica migrations do EF Core antes dos testes de integração no CI ([a0ad842](https://github.com/pedrozulian/mouts-sales-api/commit/a0ad842110c1c654f7531e9903128534f6d1544c))
* evita DbUpdateConcurrencyException ao adicionar itens durante a reconciliação ([eba7a72](https://github.com/pedrozulian/mouts-sales-api/commit/eba7a72f038011d2abe250f4af2757e3aeaf8736))
* evita expansão direta de secrets nos blocos run do job sonar ([bec2429](https://github.com/pedrozulian/mouts-sales-api/commit/bec24298b82d603f47a51dc8c970607dab802978))
* fixa actions de terceiros por commit sha para evitar comprometimento de supply chain ([5c6d081](https://github.com/pedrozulian/mouts-sales-api/commit/5c6d081515142ab57ab4c602b7efbab8746ced37))
* passa a exceção capturada ao logger de conflito de concorrência ([acef62f](https://github.com/pedrozulian/mouts-sales-api/commit/acef62f8ddbdccd039455a52f46a311e3b1bb079))
