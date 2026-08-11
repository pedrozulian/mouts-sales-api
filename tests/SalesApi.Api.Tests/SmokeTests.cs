using SalesApi.Api.Tests.Infrastructure;

namespace SalesApi.Api.Tests;

public class SmokeTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public SmokeTests(SalesApiFactory factory)
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
