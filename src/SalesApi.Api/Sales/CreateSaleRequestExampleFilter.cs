using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using SalesApi.Application.Sales.Dtos;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SalesApi.Api.Sales;

public sealed class CreateSaleRequestExampleFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(CreateSaleRequest))
        {
            return;
        }

        schema.Example = new OpenApiObject
        {
            ["saleDate"] = new OpenApiString("2026-08-09T14:30:00Z"),
            ["customer"] = new OpenApiObject
            {
                ["id"] = new OpenApiString("9f1c8f2a-0000-0000-0000-000000000001"),
                ["name"] = new OpenApiString("Maria Souza"),
            },
            ["branch"] = new OpenApiObject
            {
                ["id"] = new OpenApiString("3a7d1b04-0000-0000-0000-000000000002"),
                ["name"] = new OpenApiString("Filial Centro"),
            },
            ["items"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["product"] = new OpenApiObject
                    {
                        ["id"] = new OpenApiString("c02b0000-0000-0000-0000-000000000003"),
                        ["name"] = new OpenApiString("Teclado Mecânico K68"),
                    },
                    ["quantity"] = new OpenApiInteger(10),
                    ["unitPrice"] = new OpenApiDouble(250.00),
                },
            },
        };
    }
}
