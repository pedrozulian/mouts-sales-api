using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Infrastructure;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Api.Tests.Infrastructure;

public class DependencyInjectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_SemConnectionString_DeveLancarInvalidOperationException(string? connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
            })
            .Build();

        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_ComConnectionStringValida_NaoDeveLancarExcecaoERegistrarAppDbContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=salesapi;Username=salesapi;Password=salesapi",
            })
            .Build();

        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AppDbContext));
    }

    [Theory]
    [InlineData("migrator")]
    [InlineData("MIGRATOR")]
    public void AddInfrastructure_ArtefatoMigratorSemConnectionString_NaoDeveLancarExcecao(string artifact)
    {
        // Ver Guia Técnico, seção 14, para o porquê desta exceção.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = null,
                ["SalesApi:Artifact"] = artifact,
            })
            .Build();

        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AppDbContext));
    }

    [Fact]
    public void AddInfrastructure_ArtefatoDiferenteDeMigratorSemConnectionString_DeveLancarExcecao()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = null,
                ["SalesApi:Artifact"] = "api",
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));
    }
}
