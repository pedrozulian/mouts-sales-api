using Microsoft.EntityFrameworkCore;
using SalesApi.Infrastructure.Persistence;

namespace SalesApi.Api.Tests.Infrastructure;

public class AppDbContextConnectionTests
{
    [Fact]
    public async Task AppDbContext_DeveConseguirAbrirConexaoComPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=salesapi;Username=salesapi;Password=salesapi";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);

        var canConnect = await dbContext.Database.CanConnectAsync();

        Assert.True(canConnect);
    }
}
