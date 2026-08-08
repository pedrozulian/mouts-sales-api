using Microsoft.AspNetCore.Mvc.Testing;

namespace SalesApi.Api.Tests;

public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Host_DeveSubirSemExcecoes()
    {
        using var client = _factory.CreateClient();

        Assert.NotNull(client);
    }
}
