using FluentAssertions;
using Healthcare.Presentation.API.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Healthcare.UnitTests.Presentation.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Production_SetsAllRequiredSecurityHeaders()
    {
        var context = CreateHttpsContext();
        var middleware = new SecurityHeadersMiddleware(
            next: async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("{\"ok\":true}");
            },
            environment: new FakeHostEnvironment(Environments.Production));

        await middleware.InvokeAsync(context);

        var h = context.Response.Headers;
        h["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        h["X-Frame-Options"].ToString().Should().Be("DENY");
        h["Referrer-Policy"].ToString().Should().Be("no-referrer");
        h["Permissions-Policy"].ToString().Should().Contain("camera=()");
        h["Permissions-Policy"].ToString().Should().Contain("geolocation=()");
        h["Content-Security-Policy"].ToString().Should().Be(SecurityHeadersMiddleware.ProductionCsp);
        h["Content-Security-Policy"].ToString().Should().Contain("frame-ancestors 'none'");
        h["Strict-Transport-Security"].ToString().Should().Be(SecurityHeadersMiddleware.HstsValue);
        h["Cache-Control"].ToString().Should().Contain("no-store");
        h["Cross-Origin-Resource-Policy"].ToString().Should().Be("same-site");
        h["X-Permitted-Cross-Domain-Policies"].ToString().Should().Be("none");
    }

    [Fact]
    public async Task Development_UsesSwaggerFriendlyCsp_AndSkipsHsts()
    {
        var context = CreateHttpsContext();
        var middleware = new SecurityHeadersMiddleware(
            next: async ctx =>
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync("ok");
            },
            environment: new FakeHostEnvironment(Environments.Development));

        await middleware.InvokeAsync(context);

        var h = context.Response.Headers;
        h["Content-Security-Policy"].ToString().Should().Be(SecurityHeadersMiddleware.DevelopmentCsp);
        h["X-Frame-Options"].ToString().Should().Be("DENY");
        h["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        h.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task HeadersApplied_EvenWhenDownstreamThrows()
    {
        // Headers are set before next(), so they remain after a failure.
        var context = CreateHttpsContext();
        var headers = new SecurityHeadersMiddleware(
            next: _ => throw new InvalidOperationException("boom"),
            environment: new FakeHostEnvironment(Environments.Production));

        var act = () => headers.InvokeAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>();

        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["Content-Security-Policy"].ToString()
            .Should().Contain("default-src 'none'");
    }

    [Fact]
    public async Task DoesNotOverwriteExistingHeader()
    {
        var context = CreateHttpsContext();
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        var middleware = new SecurityHeadersMiddleware(
            next: async ctx =>
            {
                await ctx.Response.WriteAsync("ok");
            },
            environment: new FakeHostEnvironment(Environments.Production));

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
    }

    [Fact]
    public async Task Staging_NonDevelopment_GetsProductionCsp()
    {
        var context = CreateHttpsContext();
        var middleware = new SecurityHeadersMiddleware(
            next: async ctx => await ctx.Response.WriteAsync("ok"),
            environment: new FakeHostEnvironment("Staging"));

        await middleware.InvokeAsync(context);

        context.Response.Headers["Content-Security-Policy"].ToString()
            .Should().Be(SecurityHeadersMiddleware.ProductionCsp);
        context.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be(SecurityHeadersMiddleware.HstsValue);
    }

    private static DefaultHttpContext CreateHttpsContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
