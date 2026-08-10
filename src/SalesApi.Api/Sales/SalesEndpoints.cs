using Mapster;
using MediatR;
using SalesApi.Application.Sales.Create;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Application.Sales.Get;

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

        app.MapGet("/api/sales/{id:guid}", GetSale)
            .WithName("GetSale")
            .WithTags("Sales")
            .WithSummary("Consulta uma venda pelo identificador")
            .WithDescription(
                "Retorna a venda completa — cliente, filial, itens ativos e cancelados, descontos e totais já " +
                "calculados — sem recalcular nada e sem consultar nenhum outro serviço.")
            .Produces<SaleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

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

    private static async Task<IResult> GetSale(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaleQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return Results.NotFound(new
            {
                errors = result.Errors.Select(error => new { key = error.Key, message = error.Message }),
            });
        }

        return Results.Ok(result.Value);
    }
}
