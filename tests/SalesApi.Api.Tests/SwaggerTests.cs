using SalesApi.Api.Tests.Infrastructure;

namespace SalesApi.Api.Tests;

public class SwaggerTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public SwaggerTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SwaggerJson_DeveRetornar200EListarEndpointHealth()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"/health\"", json);
    }
}
