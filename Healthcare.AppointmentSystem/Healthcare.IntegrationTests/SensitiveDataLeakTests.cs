using System.Net;
using Asp.Versioning;
using FluentAssertions;
using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.CompleteAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.MarkNoShowAppointment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Facades;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Application.Queries.GetAppointment;
using Healthcare.Application.Queries.GetAppointmentsByPatient;
using Healthcare.Application.Services;
using Healthcare.Presentation.API.Controllers;
using Healthcare.Presentation.API.Middleware;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

                var repoMock = new Mock<IAppointmentRepository>();
                repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new TimeoutException(
                        "Secret: Server=prod;Database=Healthcare;UID=sa;PWD=pass123!"));
                services.AddScoped<IAppointmentRepository>(_ => repoMock.Object);

                services.AddScoped<IUnitOfWork>(_ => Mock.Of<IUnitOfWork>());
                services.AddScoped<IAppointmentFacade>(_ => Mock.Of<IAppointmentFacade>());

                services.AddScoped<
                    IQueryHandler<GetAppointmentQuery, Result<AppointmentDto>>,
                    GetAppointmentHandler>();
                services.AddScoped(_ => Mock.Of<IQueryHandler<GetAppointmentsByPatientQuery, Result<IEnumerable<AppointmentDto>>>>());
                services.AddScoped(_ => Mock.Of<ICommandHandler<BookAppointmentCommand, Result<int>>>());
                services.AddScoped(_ => Mock.Of<ICommandHandler<ConfirmAppointmentCommand, Result>>());
                services.AddScoped(_ => Mock.Of<ICommandHandler<CancelAppointmentCommand, Result>>());
                services.AddScoped(_ => Mock.Of<ICommandHandler<CompleteAppointmentCommand, Result>>());
                services.AddScoped(_ => Mock.Of<ICommandHandler<MarkNoShowAppointmentCommand, Result>>());
            })
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseRouting();
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
}