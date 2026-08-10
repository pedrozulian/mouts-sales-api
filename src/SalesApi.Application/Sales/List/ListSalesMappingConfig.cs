using Mapster;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Domain.Sales;

namespace SalesApi.Application.Sales.List;

public sealed class ListSalesMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Sale, SaleSummaryResponse>();
    }
}
