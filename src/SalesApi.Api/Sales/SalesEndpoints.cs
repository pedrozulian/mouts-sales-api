using Mapster;
using MediatR;
using SalesApi.Application.Common.Dtos;
using SalesApi.Application.Sales.Create;
using SalesApi.Application.Sales.Dtos;
using SalesApi.Application.Sales.Get;
using SalesApi.Application.Sales.List;
using SalesApi.Application.Sales.Update;

namespace SalesApi.Api.Sales;

public static class SalesEndpoints
{
    private const string Tag = "Sales";

    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sales", CreateSale)
            .WithName("CreateSale")
            .WithTags(Tag)
            .WithSummary("Registra uma nova venda")
            .WithDescription(
                "Recebe cliente, filial e itens (produto, quantidade, preço unitário), calcula o desconto " +
                "progressivo por faixa de quantidade e os totais, e persiste a venda em uma única transação.")
            .Produces<SaleResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/api/sales/{id:guid}", GetSale)
            .WithName("GetSale")
            .WithTags(Tag)
            .WithSummary("Consulta uma venda pelo identificador")
            .WithDescription(
                "Retorna a venda completa — cliente, filial, itens ativos e cancelados, descontos e totais já " +
                "calculados — sem recalcular nada e sem consultar nenhum outro serviço.")
            .Produces<SaleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/sales", ListSales)
            .WithName("ListSales")
            .WithTags(Tag)
            .WithSummary("Lista vendas de forma paginada")
            .WithDescription(
                "Retorna uma página de vendas em forma resumida (sem os itens), ordenada por data decrescente, " +
                "com filtros opcionais por cliente, filial e situação de cancelamento.")
            .Produces<PagedResult<SaleSummaryResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPut("/api/sales/{id:guid}", UpdateSale)
            .WithName("UpdateSale")
            .WithTags(Tag)
            .WithSummary("Altera uma venda existente")
            .WithDescription(
                "Substitui o cabeçalho (cliente, filial, data) de uma venda ativa e reconcilia seus itens: item " +
                "com id conhecido é atualizado, item sem id é adicionado, item ausente do corpo é " +
                "cancelado logicamente.")
            .Produces<SaleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
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

    private static async Task<IResult> ListSales(
        string? page,
        string? pageSize,
        string? customerId,
        string? branchId,
        string? isCancelled,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new ListSalesQuery(page, pageSize, customerId, branchId, isCancelled);

        var result = await sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return Results.BadRequest(new
            {
                errors = result.Errors.Select(error => new { key = error.Key, message = error.Message }),
            });
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateSale(
        Guid id,
        UpdateSaleRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<UpdateSaleCommand>() with { Id = id };

        var result = await sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            var errors = result.Errors.Select(error => new { key = error.Key, message = error.Message });

            return result.Errors.Any(error => error.Key == "id")
                ? Results.NotFound(new { errors })
                : Results.BadRequest(new { errors });
        }

        return Results.Ok(result.Value);
    }
}
