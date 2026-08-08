using Mapster;
using SalesApi.Application.Common;

namespace SalesApi.Application.Tests.Common;

public class MapsterConfigurationTests
{
    [Fact]
    public void SampleSource_DeveSerMapeadoParaSampleDestination()
    {
        var config = new TypeAdapterConfig();
        config.Scan(typeof(SampleMappingConfig).Assembly);

        var source = new SampleSource { Name = "Sales Api" };

        var destination = source.Adapt<SampleDestination>(config);

        Assert.Equal(source.Name, destination.Name);
    }
}
