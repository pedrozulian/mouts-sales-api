using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalesApi.Application.Common;

namespace SalesApi.Api.Tests.Infrastructure;

public class ErrorHandlingTests : IClassFixture<SalesApiFactory>
{
    private readonly SalesApiFactory _factory;

    public ErrorHandlingTests(SalesApiFactory factory)
    {
        _factory = factory;
    }

    // Dublê que força uma exceção não tratada logo no início do handler — antes de qualquer
    // acesso a banco —, simulando uma falha de infraestrutura de forma determinística.
    private sealed class ThrowingSaleNumberGenerator : ISaleNumberGenerator
    {
        public Task<string> NextAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Falha simulada de infraestrutura para teste.");
    }

    private static object ValidPayload() => new
    {
        customer = new { id = Guid.NewGuid(), name = "Maria Souza" },
        branch = new { id = Guid.NewGuid(), name = "Filial Centro" },
        items = new[]
        {
            new { product = new { id = Guid.NewGuid(), name = "Item" }, quantity = 1, unitPrice = 10.00m },
        },
    };

    [Fact]
    public async Task PostSales_ComExcecaoNaoTratada_DeveRetornar500NoContratoDeErroSemDetalheInterno()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
                services.AddScoped<ISaleNumberGenerator, ThrowingSaleNumberGenerator>());
        });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/sales", ValidPayload());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("Falha simulada de infraestrutura", body);
        Assert.DoesNotContain("   at ", body);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(errorResponse);
        Assert.Contains(errorResponse!.Errors, e => e.Key == "server");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _provider;

            public CapturingLogger(CapturingLoggerProvider provider) => _provider = provider;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _provider.Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }

    [Fact]
    public async Task PostSales_ComExcecaoNaoTratada_DeveRegistrarACausaOriginalViaLogEstruturado()
    {
        var loggerProvider = new CapturingLoggerProvider();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
                services.AddScoped<ISaleNumberGenerator, ThrowingSaleNumberGenerator>());
            builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
        });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/sales", ValidPayload());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            loggerProvider.Entries,
            e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    private sealed record ErrorResponse(ErrorItem[] Errors);

    private sealed record ErrorItem(string Key, string Message);
}
