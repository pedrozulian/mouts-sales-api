using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Application.Common;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // O fail-fast abaixo não se aplica ao migrator — ver Guia Técnico, seção 14.
        var isMigratorArtifact = string.Equals(
            configuration["SalesApi:Artifact"],
            "migrator",
            StringComparison.OrdinalIgnoreCase);

        if (!isMigratorArtifact && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' não configurada. Defina a variável de " +
                "ambiente ConnectionStrings__DefaultConnection apontando para o PostgreSQL.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ISaleNumberGenerator, SaleNumberGenerator>();

        return services;
    }
}
