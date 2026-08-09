using Mapster;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Sales.Create;

public sealed class CreateSaleMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ExternalReferenceRequest, ExternalReference>()
            .MapWith(src => new ExternalReference(src.Id, src.Name));

        config.NewConfig<ExternalReference, ExternalReferenceResponse>();

        config.NewConfig<SaleItemRequest, SaleItemInput>();

        config.NewConfig<SaleItem, SaleItemResponse>();

        config.NewConfig<Sale, SaleResponse>();
    }
}
