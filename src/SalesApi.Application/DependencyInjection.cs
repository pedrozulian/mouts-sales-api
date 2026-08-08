using System.Reflection;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace SalesApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        services.AddMapster();

        return services;
    }
}
