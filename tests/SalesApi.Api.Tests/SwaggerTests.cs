using Microsoft.AspNetCore.Mvc.Testing;

namespace SalesApi.Api.Tests;

public class SwaggerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SwaggerTests(WebApplicationFactory<Program> factory)
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
