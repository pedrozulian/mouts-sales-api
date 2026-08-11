using Mapster;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Sales.Update;

public sealed class UpdateSaleMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<SaleItemChangeRequest, SaleItemChangeInput>();
    }
}
