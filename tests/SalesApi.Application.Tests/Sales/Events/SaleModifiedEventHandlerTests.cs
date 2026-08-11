using Microsoft.Extensions.Logging;
using SalesApi.Application.Sales.Events;
using SalesApi.Domain.Sales.Events;

namespace SalesApi.Application.Tests.Sales.Events;

public class SaleModifiedEventHandlerTests
{
    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public async Task Handle_DeveLogarSaleIdSaleNumberETotalAmount()
    {
        var logger = new FakeLogger<SaleModifiedEventHandler>();
        var handler = new SaleModifiedEventHandler(logger);
        var notification = new SaleModified(Guid.NewGuid(), "V-000123", 2400.00m);

        await handler.Handle(notification, CancellationToken.None);

        Assert.Contains(
            logger.Messages,
            m => m.Contains(notification.SaleId.ToString()) && m.Contains(notification.SaleNumber) && m.Contains("2400"));
    }
}
