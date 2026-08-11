using Xunit;

// A connection string do Postgres de teste é injetada via variável de ambiente (processo
// inteiro, ver SalesApiFactory) — com classes rodando em paralelo, uma classe poderia
// sobrescrever a connection string de outra entre o InitializeAsync e o primeiro acesso ao
// host. Serializar as classes de teste evita essa corrida.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
