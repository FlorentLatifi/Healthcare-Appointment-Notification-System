using System.Net;
using FluentAssertions;
using Healthcare.Presentation.API.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
    }

    [Fact]
    public async Task DbUpdateException_Returns409ConflictWithCleanMessage()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw new DbUpdateException("Cannot insert duplicate key"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);

        var body = await ReadBodyAsync(context);
        body.Should().Contain("This record cannot be removed because related records exist.");
    }

    [Fact]
    public async Task ArgumentException_StillReturns400()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw new ArgumentException("Invalid argument"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Exception_Returns500WithTypeAndMessageInResponse()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Something broke"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        var body = await ReadBodyAsync(context);
        body.Should().Contain("Something broke");
        body.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public async Task Unexpected500Exception_InProduction_HidesSensitiveMessage()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(
            _ => throw new ArithmeticException("Internal: DB server=prod01;UID=sa;PWD=secret123"),
            environmentName: "Production");

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);

        var body = await ReadBodyAsync(context);
        body.Should().NotContain("prod01");
        body.Should().NotContain("PWD=secret123");
        body.Should().Contain("An internal server error occurred");
        body.Should().NotContain("StackTrace");
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(
        Func<HttpContext, Task> nextAction,
        string environmentName = "Development")
    {
        var next = new RequestDelegate(nextAction);
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>().Object;
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return new ExceptionHandlingMiddleware(next, logger, env.Object);
    }

    private static DefaultHttpContext CreateContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Path = "/api/test";
        context.Request.Method = "GET";
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
