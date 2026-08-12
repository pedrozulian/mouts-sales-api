using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SalesApi.Api.Common;
using SalesApi.Domain.Common;

namespace SalesApi.Api.Tests.Common;

public class ResultExtensionsTests
{
    [Fact]
    public void ToHttpResult_ComSucesso_DeveRetornarOk()
    {
        var result = Result<string>.Success("valor");

        var httpResult = result.ToHttpResult();

        var statusCodeResult = Assert.IsType<IStatusCodeHttpResult>(httpResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
    }

    [Fact]
    public void ToHttpResult_ComFalhaEChaveForaDasDeNaoEncontrado_DeveRetornarBadRequest()
    {
        var result = Result<string>.Failure(new Notification("campo", "erro de validação"));

        var httpResult = result.ToHttpResult("id");

        var statusCodeResult = Assert.IsType<IStatusCodeHttpResult>(httpResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);
    }

    [Fact]
    public void ToHttpResult_ComFalhaEChaveEntreAsDeNaoEncontrado_DeveRetornarNotFound()
    {
        var result = Result<string>.Failure(new Notification("id", "Venda não encontrada."));

        var httpResult = result.ToHttpResult("id");

        var statusCodeResult = Assert.IsType<IStatusCodeHttpResult>(httpResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status404NotFound, statusCodeResult.StatusCode);
    }

    [Fact]
    public void ToNoContentResult_ComSucesso_DeveRetornarNoContent()
    {
        var result = Result.Success();

        var httpResult = result.ToNoContentResult();

        var statusCodeResult = Assert.IsType<IStatusCodeHttpResult>(httpResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status204NoContent, statusCodeResult.StatusCode);
    }

    [Fact]
    public void ToNoContentResult_ComFalhaEChaveEntreVariasDeNaoEncontrado_DeveRetornarNotFound()
    {
        var result = Result.Failure(new Notification("itemId", "Item não encontrado nesta venda."));

        var httpResult = result.ToNoContentResult("id", "itemId");

        var statusCodeResult = Assert.IsType<IStatusCodeHttpResult>(httpResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status404NotFound, statusCodeResult.StatusCode);
    }

    [Fact]
    public void ToNoContentResult_ComFalhaEChaveForaDasDeNaoEncontrado_DeveRetornarBadRequest()
    {
        var result = Result.Failure(new Notification("sale", "Venda já está cancelada."));

        var httpResult = result.ToNoContentResult("id", "itemId");

        var statusCodeResult = Assert.IsType<IStatusCodeHttpResult>(httpResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);
    }
}
