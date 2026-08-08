using Mapster;

namespace SalesApi.Application.Common;

public sealed class SampleSource
{
    public string Name { get; init; } = string.Empty;
}

public sealed class SampleDestination
{
    public string Name { get; init; } = string.Empty;
}

public sealed class SampleMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<SampleSource, SampleDestination>();
    }
}
