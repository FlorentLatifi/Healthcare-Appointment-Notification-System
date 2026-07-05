using System.Reflection;
using FluentAssertions;
using Healthcare.Application.Ports.Events;
using Healthcare.Domain.Common;
using NetArchTest.Rules;

namespace Healthcare.UnitTests.Architecture;

public sealed class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Healthcare.Application.Common.Result).Assembly;
    private static readonly Assembly AdaptersAssembly = typeof(Healthcare.Adapters.Persistence.InMemory.InMemoryUnitOfWork).Assembly;
    private static readonly Assembly PresentationAssembly = typeof(Healthcare.Presentation.API.Controllers.AppointmentsController).Assembly;

    [Fact]
    public void DomainLayer_ShouldNotDependOnApplicationOrAdaptersOrPresentation()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace("Healthcare.Domain")
            .ShouldNot()
            .HaveDependencyOn("Healthcare.Application")
            .And().HaveDependencyOn("Healthcare.Adapters")
            .And().HaveDependencyOn("Healthcare.Presentation.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOnAdaptersOrPresentation()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("Healthcare.Application")
            .ShouldNot()
            .HaveDependencyOn("Healthcare.Adapters")
            .And().HaveDependencyOn("Healthcare.Presentation.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void AdaptersLayer_ShouldNotDependOnPresentation()
    {
        var result = Types.InAssembly(AdaptersAssembly)
            .That().ResideInNamespace("Healthcare.Adapters")
            .ShouldNot()
            .HaveDependencyOn("Healthcare.Presentation.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainEventHandlers_ShouldNotLiveInApplicationLayer()
    {
        var violatingTypes = Types.InAssembly(ApplicationAssembly)
            .That().ImplementInterface(typeof(IDomainEventHandler<>))
            .GetTypes();

        violatingTypes.Should().BeEmpty("IDomainEventHandler implementations should live in Healthcare.Adapters, not Healthcare.Application");
    }

    [Fact]
    public void DomainEntities_ShouldNotDependOnEntityFrameworkCoreOrSystemData()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(Entity))
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .And().HaveDependencyOn("System.Data")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
