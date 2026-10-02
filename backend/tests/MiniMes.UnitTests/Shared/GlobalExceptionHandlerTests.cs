using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniMes.Api.Shared.Http;
using Npgsql;

namespace MiniMes.UnitTests.Shared;

public class GlobalExceptionHandlerTests
{
    private static GlobalExceptionHandler NewHandler() => new(NullLogger<GlobalExceptionHandler>.Instance);

    private static DefaultHttpContext NewContext() => new() { Response = { Body = new MemoryStream() } };

    private static string ReadBody(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        return new StreamReader(ctx.Response.Body).ReadToEnd();
    }

    private static PostgresException UniqueViolation() =>
        new("duplicate key", "ERROR", "ERROR", "23505");

    [Fact]
    public async Task Concurrency_exception_becomes_409_concurrency_conflict()
    {
        var ctx = NewContext();
        var handled = await NewHandler().TryHandleAsync(ctx, new DbUpdateConcurrencyException(), TestContext.Current.CancellationToken);
        Assert.True(handled);
        Assert.Equal(409, ctx.Response.StatusCode);
        Assert.Contains("CONCURRENCY_CONFLICT", ReadBody(ctx));
    }

    [Fact]
    public async Task Unique_violation_becomes_409_concurrency_conflict()
    {
        var ctx = NewContext();
        var handled = await NewHandler().TryHandleAsync(ctx, UniqueViolation(), TestContext.Current.CancellationToken);
        Assert.True(handled);
        Assert.Equal(409, ctx.Response.StatusCode);
        Assert.Contains("CONCURRENCY_CONFLICT", ReadBody(ctx));
    }

    [Fact]
    public async Task Unique_violation_wrapped_in_update_exception_becomes_409()
    {
        var ctx = NewContext();
        var handled = await NewHandler().TryHandleAsync(
            ctx, new DbUpdateException("save failed", UniqueViolation()), TestContext.Current.CancellationToken);
        Assert.True(handled);
        Assert.Equal(409, ctx.Response.StatusCode);
        Assert.Contains("CONCURRENCY_CONFLICT", ReadBody(ctx));
    }

    [Fact]
    public async Task Other_exception_becomes_500_without_stack_trace()
    {
        var ctx = NewContext();
        var handled = await NewHandler().TryHandleAsync(ctx, new InvalidOperationException("boom secret"), TestContext.Current.CancellationToken);
        Assert.True(handled);
        Assert.Equal(500, ctx.Response.StatusCode);
        var body = ReadBody(ctx);
        Assert.DoesNotContain("boom secret", body);
        Assert.DoesNotContain(" at ", body);
        Assert.DoesNotContain("CONCURRENCY_CONFLICT", body);
    }
}
