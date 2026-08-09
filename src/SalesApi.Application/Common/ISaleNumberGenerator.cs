namespace SalesApi.Application.Common;

public interface ISaleNumberGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
