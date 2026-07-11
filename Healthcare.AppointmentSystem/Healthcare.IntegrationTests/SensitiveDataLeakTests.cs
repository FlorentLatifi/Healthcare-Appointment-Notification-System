using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Asp.Versioning;
using FluentAssertions;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Presentation.API.Controllers;
using Healthcare.Presentation.API.Middleware;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Healthcare.IntegrationTests;

public sealed class SensitiveDataLeakTests
{
    [Fact]
    public async Task UnexpectedException_FromEndpoint_IsSanitizedInProduction()
    {
        using var server = new TestServer(new WebHostBuilder()
            .UseEnvironment("Production")
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.Run(_ => throw new TimeoutException(
                    "Secret: Server=prod;Database=Healthcare;UID=sa;PWD=pass123!"));
            }));

        var client = server.CreateClient();
        var response = await client.GetAsync("/test");

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("Secret");
        body.Should().NotContain("Server=prod");
        body.Should().NotContain("PWD=pass123");
        body.Should().Contain("An internal server error occurred");
        body.Should().NotContain("StackTrace");
    }

    [Fact]
    public async Task UnexpectedException_ThroughControllerPipeline_IsSanitizedInProduction()
    {
        using var server = new TestServer(new WebHostBuilder()
            .UseEnvironment("Production")
            .ConfigureServices(services =>
            {
                var mvcBuilder = services.AddControllers();
                mvcBuilder.PartManager.ApplicationParts.Add(
                    new AssemblyPart(typeof(AppointmentsController).Assembly));

                services.AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                })
                .AddMvc()
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

                services.AddRouting();
                services.AddLogging();

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .Build();
                });

                services.AddScoped<IUnitOfWork>(_ => Mock.Of<IUnitOfWork>());
                services.AddScoped<IDomainEventDispatcher>(_ => Mock.Of<IDomainEventDispatcher>());

                // Controller dispatches GetAppointment via MediatR; force a secret-bearing exception.
                var mediatorMock = new Mock<IMediator>();
                mediatorMock
                    .Setup(m => m.Send(It.IsAny<GetAppointmentQuery>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new TimeoutException(
                        "Secret: Server=prod;Database=Healthcare;UID=sa;PWD=pass123!"));
                services.AddScoped<IMediator>(_ => mediatorMock.Object);

                services.AddScoped(_ => Mock.Of<ICommandHandler<CancelAppointmentCommand, Result>>());
                services.AddScoped(_ => Mock.Of<ICommandHandler<CompleteAppointmentCommand, Result>>());
                services.AddScoped(_ => Mock.Of<ICommandHandler<MarkNoShowAppointmentCommand, Result>>());
            })
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));

        var client = server.CreateClient();
        var response = await client.GetAsync("/api/v1/appointments/99999");

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain("Secret");
        body.Should().NotContain("Server=prod");
        body.Should().NotContain("PWD=pass123");
        body.Should().Contain("An internal server error occurred");
        body.Should().NotContain("StackTrace");
    }

    /// <summary>Authenticates every request as Admin for isolated pipeline tests.</summary>
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim(ClaimTypes.Name, "test-admin")
                },
                Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}