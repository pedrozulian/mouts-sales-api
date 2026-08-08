using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SalesApi.Application.Common;

namespace SalesApi.Application.Tests.Common;

public class MediatorRegistrationTests
{
    [Fact]
    public async Task PingQuery_DeveSerDespachadaEResponderViaIMediator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        await using var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var response = await mediator.Send(new PingQuery());

        Assert.Equal("pong", response);
    }
}
