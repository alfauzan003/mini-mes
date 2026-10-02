using Microsoft.AspNetCore.Http.HttpResults;
using MiniMes.Api.Shared.Results;

namespace MiniMes.UnitTests.Shared;

public class ResultHttpExtensionsTests
{
    [Fact]
    public void Rule_error_maps_to_422_problem_with_error_code()
    {
        Result<int> result = new Error(ErrorCodes.LotNotAvailable, "Lot is not waiting");
        var problem = Assert.IsType<ProblemHttpResult>(result.ToHttpResult());
        Assert.Equal(422, problem.StatusCode);
        Assert.Equal("LOT_NOT_AVAILABLE", problem.ProblemDetails.Extensions["errorCode"]);
        Assert.Equal("Lot is not waiting", problem.ProblemDetails.Detail);
    }

    [Theory]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.Unauthorized, 401)]
    public void Error_kind_maps_to_status(ErrorKind kind, int status)
    {
        Result result = new Error("X", "x", kind);
        Assert.Equal(status, Assert.IsType<ProblemHttpResult>(result.ToHttpResult()).StatusCode);
    }

    [Fact]
    public void Success_maps_to_ok_with_value()
    {
        Result<int> result = 42;
        Assert.Equal(42, Assert.IsType<Ok<int>>(result.ToHttpResult()).Value);
    }
}
