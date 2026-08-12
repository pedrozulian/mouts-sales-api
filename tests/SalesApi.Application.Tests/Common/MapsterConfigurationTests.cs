using Mapster;
using SalesApi.Application.Sales.Create;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Tests.Common;

public class MapsterConfigurationTests
{
    [Fact]
    public void ExternalReferenceRequest_DeveSerMapeadoParaExternalReference()
    {
        var config = new TypeAdapterConfig();
        config.Scan(typeof(CreateSaleMappingConfig).Assembly);

        var source = new ExternalReferenceRequest(Guid.NewGuid(), "Maria Souza");

        var destination = source.Adapt<ExternalReference>(config);

        Assert.Equal(source.Id, destination.Id);
        Assert.Equal(source.Name, destination.Name);
    }
}
