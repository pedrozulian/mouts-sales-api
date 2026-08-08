using Microsoft.AspNetCore.Mvc.Testing;

namespace SalesApi.Api.Tests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_DeveRetornar200ComPostgresqlSaudavelQuandoBancoEstaAcessivel()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Healthy\"", json);
        Assert.Contains("\"postgresql\"", json);
    }
}
