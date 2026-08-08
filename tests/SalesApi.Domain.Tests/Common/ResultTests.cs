using SalesApi.Domain.Common;

namespace SalesApi.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_DeveExporIsSuccessVerdadeiroSemErros()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_DeveExporIsSuccessFalsoComOsErrosInformados()
    {
        var error = new Notification("campo", "mensagem de erro");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains(result.Errors, e => e.Key == "campo" && e.Message == "mensagem de erro");
    }
}
