using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SalesApi.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => BuildCheck(entry.Key, entry.Value))
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, SerializerOptions),
            context.RequestAborted);
    }

    private static object BuildCheck(string name, HealthReportEntry entry)
    {
        if (entry.Status == HealthStatus.Healthy)
        {
            return new { name, status = entry.Status.ToString() };
        }

        return new
        {
            name,
            status = entry.Status.ToString(),
            description = entry.Exception?.Message ?? entry.Description ?? "Falha desconhecida"
        };
    }
}
