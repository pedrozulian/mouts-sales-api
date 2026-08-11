using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalesApi.Api.Tests.Infrastructure;

// Cada classe de teste (via IClassFixture<SalesApiFactory>) recebe seu próprio container
// Postgres efêmero — sem estado compartilhado entre classes nem entre execuções locais
// sucessivas. O container (e seus dados) é descartado ao final da classe de teste.
//
// A connection string é injetada via variável de ambiente (não via ConfigureWebHost /
// ConfigureAppConfiguration): em Program.cs (Minimal API), `AddNpgSql` e `AddInfrastructure`
// leem `builder.Configuration.GetConnectionString(...)` e capturam o valor por cópia ANTES do
// host ser efetivamente construído — nesse ponto, overrides de IConfiguration registrados via
// ConfigureWebHost já chegam tarde demais. Variáveis de ambiente, por outro lado, já são lidas
// pelo provider padrão dentro do próprio WebApplication.CreateBuilder(args), antes de qualquer
// linha do Program.cs rodar — por isso, setá-la antes do host ser criado é a única forma
// confiável de sobrescrever a connection string neste modelo de hospedagem.
public sealed class SalesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("salesapi_test")
        .WithUsername("salesapi")
        .WithPassword("salesapi")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
