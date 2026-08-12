using Microsoft.Extensions.Diagnostics.HealthChecks;
using SalesApi.Api.ErrorHandling;
using SalesApi.Api.HealthChecks;
using SalesApi.Api.Sales;
using SalesApi.Application;
using SalesApi.Infrastructure;
using SalesApi.Infrastructure.HealthChecks;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(),
    writeToProviders: true);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SchemaFilter<CreateSaleRequestExampleFilter>());

builder.Services
    .AddHealthChecks()
    .AddCheck<PendingMigrationsHealthCheck>("postgresql");

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

const string CorrelationIdHeader = "X-Correlation-Id";

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue)
        && !string.IsNullOrWhiteSpace(headerValue)
        ? headerValue.ToString()
        : Guid.NewGuid().ToString();

    context.Response.Headers[CorrelationIdHeader] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseExceptionHandler();

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapGet("/health", async (HealthCheckService healthCheckService, HttpContext context) =>
{
    var report = await healthCheckService.CheckHealthAsync();

    context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
        ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status200OK;

    await HealthCheckResponseWriter.WriteResponse(context, report);
});

app.MapSalesEndpoints();

await app.RunAsync();

public partial class Program
{
    protected Program()
    {
    }
}
