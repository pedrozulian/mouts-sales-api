using Mapster;
using MediatR;
using SalesApi.Application.Sales.Create;
using SalesApi.Application.Sales.Dtos;

namespace SalesApi.Api.Sales;

public static class SalesEndpoints
{
    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sales", CreateSale)
            .WithName("CreateSale")
            .WithTags("Sales")
            .WithSummary("Registra uma nova venda")
            .WithDescription(
                "Recebe cliente, filial e itens (produto, quantidade, preço unitário), calcula o desconto " +
                "progressivo por faixa de quantidade e os totais, e persiste a venda em uma única transação.")
            .Produces<SaleResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateSale(
        CreateSaleRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<CreateSaleCommand>();

        var result = await sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new
            {
                errors = result.Errors.Select(error => new { key = error.Key, message = error.Message }),
            });
        }

        return Results.Created($"/api/sales/{result.Value!.Id}", result.Value);
    }
}
