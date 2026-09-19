using System.Reflection;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Common;
using NetArchTest.Rules;

namespace Healthcare.ArchitectureTests;

/// <summary>
/// Clean Architecture governance rules enforced by NetArchTest + project-reference analysis.
/// Failures block CI (see architecture-tests job in .github/workflows/ci.yml).
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Entity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Healthcare.Application.Common.Result).Assembly;
    private static readonly Assembly AdaptersAssembly = typeof(Healthcare.Adapters.AdapterServiceExtensions).Assembly;
    private static readonly Assembly PresentationAssembly = typeof(Healthcare.Presentation.API.Controllers.AppointmentsController).Assembly;

    private const string DomainNs = "Healthcare.Domain";
    private const string ApplicationNs = "Healthcare.Application";
    private const string AdaptersNs = "Healthcare.Adapters";
    private const string PresentationNs = "Healthcare.Presentation.API";

    // Presentation types allowed to touch Adapters (composition root only).
    private static readonly HashSet<string> CompositionRootTypeNameAllowList = new(StringComparer.Ordinal)
    {
        // Top-level Program (SDK-style minimal hosting)
        "Program",
        "Healthcare.Presentation.API.Services.DatabaseSeeder",
        // Hosted worker registered from the composition root (uses adapter ports + health state).
        "Healthcare.Presentation.API.Services.AppointmentReminderBackgroundService",
    };

    private static readonly string[] CompositionRootNamespaceAllowList =
    {
        "Healthcare.Presentation.API.Configuration",
        // Health checks resolve adapter worker health state from DI — composition-root surface only.
        "Healthcare.Presentation.API.HealthChecks",
    };

    #region Layer dependency rules (NetArchTest)

    [Fact]
    public void Domain_Should_Not_Depend_On_Application_Adapters_Or_Presentation()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(DomainNs)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNs)
            .And().HaveDependencyOn(AdaptersNs)
            .And().HaveDependencyOn(PresentationNs)
            .GetResult();

        AssertArch(result, "Domain must not depend on Application, Adapters, or Presentation.");
    }

    [Fact]
    public void Application_Should_Only_Depend_On_Domain_Among_Solution_Layers()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(ApplicationNs)
            .ShouldNot()
            .HaveDependencyOn(AdaptersNs)
            .And().HaveDependencyOn(PresentationNs)
            .GetResult();

        AssertArch(result, "Application must not depend on Adapters or Presentation (only Domain + packages).");
    }

    [Fact]
    public void Adapters_Should_Not_Depend_On_Presentation()
    {
        var result = Types.InAssembly(AdaptersAssembly)
            .That().ResideInNamespace(AdaptersNs)
            .ShouldNot()
            .HaveDependencyOn(PresentationNs)
            .GetResult();

        AssertArch(result, "Adapters must not depend on Presentation.");
    }

    [Fact]
    public void Domain_Entities_Should_Not_Depend_On_EF_Or_System_Data()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(Entity))
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .And().HaveDependencyOn("System.Data")
            .GetResult();

        AssertArch(result, "Domain entities must stay persistence-ignorant.");
    }

    [Fact]
    public void DomainEventHandlers_Should_Live_In_Adapters_Not_Application()
    {
        var inApplication = Types.InAssembly(ApplicationAssembly)
            .That().ImplementInterface(typeof(IDomainEventHandler<>))
            .GetTypes()
            .ToList();

        inApplication.Should().BeEmpty(
            "IDomainEventHandler<> implementations belong in Adapters, not Application. Found: {0}",
            string.Join(", ", inApplication.Select(t => t.FullName)));
    }

    #endregion

    #region Presentation ↔ Adapters (composition root only)

    [Fact]
    public void Presentation_Types_Should_Not_Reference_Adapters_Except_Composition_Root()
    {
        var violators = Types.InAssembly(PresentationAssembly)
            .That().ResideInNamespace(PresentationNs)
            .And().HaveDependencyOn(AdaptersNs)
            .GetTypes()
            .Where(t => !IsCompositionRoot(t))
            .Select(t => t.FullName ?? t.Name)
            .OrderBy(n => n)
            .ToList();

        violators.Should().BeEmpty(
            "Only composition-root types may reference Healthcare.Adapters. Violators: {0}",
            string.Join(", ", violators));
    }

    private static bool IsCompositionRoot(Type type)
    {
        var fullName = type.FullName ?? type.Name;
        if (CompositionRootTypeNameAllowList.Contains(fullName) ||
            CompositionRootTypeNameAllowList.Contains(type.Name))
            return true;

        var ns = type.Namespace ?? string.Empty;
        return CompositionRootNamespaceAllowList.Any(allowed =>
            ns.Equals(allowed, StringComparison.Ordinal) ||
            ns.StartsWith(allowed + ".", StringComparison.Ordinal));
    }

    #endregion

    #region Ports implemented in Adapters

    [Fact]
    public void Application_Repository_Ports_Should_Have_At_Least_One_Adapter_Implementation()
    {
        var portTypes = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespaceContaining("Healthcare.Application.Ports")
            .And().AreInterfaces()
            .GetTypes()
            // open generics / event handlers are checked separately
            .Where(t => t.IsPublic && !t.IsGenericTypeDefinition)
            .Where(t => t.Name.StartsWith("I", StringComparison.Ordinal))
            // Markers / DTOs that are not adapter-implemented services
            .Where(t => t != typeof(IDomainEventHandler<>))
            .ToList();

        // Infrastructure ports that must be satisfied by Adapters (not pure application services).
        var requiredPorts = portTypes
            .Where(t => t.IsInterface)
            .Where(t =>
                t.Namespace?.Contains(".Repositories", StringComparison.Ordinal) == true ||
                t.Namespace?.Contains(".Authentication", StringComparison.Ordinal) == true ||
                t.Namespace?.Contains(".Notifications", StringComparison.Ordinal) == true ||
                t.Namespace?.Contains(".Locking", StringComparison.Ordinal) == true ||
                t.Namespace?.Contains(".Caching", StringComparison.Ordinal) == true ||
                t.Namespace?.Contains(".Common", StringComparison.Ordinal) == true ||
                t.Namespace?.Contains(".Events", StringComparison.Ordinal) == true ||
                // Payment gateway is infrastructure; reconciliation service lives in Application.
                t.Name is "IPaymentGateway")
            .Where(t => t.Name is not "IDomainEventHandler")
            .ToList();

        var missing = new List<string>();

        foreach (var port in requiredPorts)
        {
            var implementations = Types.InAssembly(AdaptersAssembly)
                .That().ImplementInterface(port)
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false })
                .ToList();

            if (implementations.Count == 0)
                missing.Add(port.FullName ?? port.Name);
        }

        // Domain event handlers (open generic): at least one closed implementation in Adapters
        var domainEventHandlers = Types.InAssembly(AdaptersAssembly)
            .That().ImplementInterface(typeof(IDomainEventHandler<>))
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .ToList();

        domainEventHandlers.Should().NotBeEmpty(
            "Adapters must implement IDomainEventHandler<> for the outbox/observer pipeline.");

        missing.Should().BeEmpty(
            "Every Application port should have an Adapters implementation. Missing: {0}",
            string.Join(", ", missing));
    }

    [Fact]
    public void Adapters_Implementations_Of_Application_Ports_Should_Reside_In_Adapters_Namespace()
    {
        var portInterfaces = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespaceContaining("Healthcare.Application.Ports")
            .And().AreInterfaces()
            .GetTypes()
            .Where(t => t is { IsPublic: true, IsGenericTypeDefinition: false })
            .ToList();

        var misplaced = new List<string>();

        foreach (var port in portInterfaces)
        {
            var impls = Types.InAssembly(AdaptersAssembly)
                .That().ImplementInterface(port)
                .GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false });

            foreach (var impl in impls)
            {
                if (impl.Namespace is null ||
                    !impl.Namespace.StartsWith(AdaptersNs, StringComparison.Ordinal))
                {
                    misplaced.Add($"{impl.FullName} implements {port.Name}");
                }
            }
        }

        misplaced.Should().BeEmpty(
            "Port implementations must live under Healthcare.Adapters.*. Misplaced: {0}",
            string.Join(", ", misplaced));
    }

    #endregion

    #region Project reference rules (csproj)

    [Fact]
    public void Domain_Csproj_Should_Not_Reference_Other_Solution_Projects()
    {
        var refs = GetProjectReferences("Healthcare.Domain");
        refs.Should().BeEmpty("Domain must not ProjectReference Application/Adapters/Presentation.");
    }

    [Fact]
    public void Application_Csproj_Should_Only_Reference_Domain()
    {
        var refs = GetProjectReferences("Healthcare.Application");
        refs.Should().ContainSingle()
            .Which.Should().EndWith("Healthcare.Domain.csproj");
        refs.Should().NotContain(r => r.Contains("Adapters", StringComparison.OrdinalIgnoreCase));
        refs.Should().NotContain(r => r.Contains("Presentation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Adapters_Csproj_Should_Reference_Application_And_Domain_Only()
    {
        var refs = GetProjectReferences("Healthcare.Adapters");
        refs.Should().HaveCount(2);
        refs.Should().Contain(r => r.Contains("Healthcare.Application.csproj", StringComparison.Ordinal));
        refs.Should().Contain(r => r.Contains("Healthcare.Domain.csproj", StringComparison.Ordinal));
        refs.Should().NotContain(r => r.Contains("Presentation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Presentation_Csproj_May_Reference_Application_And_Adapters_Only()
    {
        // Project-level Adapters reference is the composition root wiring surface.
        var refs = GetProjectReferences("Healthcare.Presentation.API");
        refs.Should().HaveCount(2);
        refs.Should().Contain(r => r.Contains("Healthcare.Application.csproj", StringComparison.Ordinal));
        refs.Should().Contain(r => r.Contains("Healthcare.Adapters.csproj", StringComparison.Ordinal));
        refs.Should().NotContain(r =>
            r.Contains("Healthcare.Domain.csproj", StringComparison.Ordinal) &&
            !r.Contains("Application")); // Domain is transitive via Application/Adapters
    }

    #endregion

    #region Helpers

    private static void AssertArch(TestResult result, string because)
    {
        if (result.IsSuccessful)
            return;

        var failing = result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>();
        var message = new StringBuilder()
            .AppendLine(because)
            .AppendLine("Failing types:")
            .AppendJoin(Environment.NewLine, failing.Select(f => "  - " + f))
            .ToString();

        result.IsSuccessful.Should().BeTrue(message);
    }

    private static IReadOnlyList<string> GetProjectReferences(string projectFolderName)
    {
        var solutionRoot = FindSolutionRoot();
        var csprojPath = Path.Combine(solutionRoot, projectFolderName, $"{projectFolderName}.csproj");
        if (projectFolderName == "Healthcare.Presentation.API")
            csprojPath = Path.Combine(solutionRoot, projectFolderName, "Healthcare.Presentation.API.csproj");

        File.Exists(csprojPath).Should().BeTrue("csproj must exist at {0}", csprojPath);

        var doc = XDocument.Load(csprojPath);
        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        return doc.Descendants(ns + "ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Replace('/', Path.DirectorySeparatorChar))
            .ToList();
    }

    private static string FindSolutionRoot()
    {
        // ArchitectureTests project directory → solution root (parent)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Healthcare.AppointmentSystem.sln")) ||
                File.Exists(Path.Combine(dir.FullName, "Healthcare.Domain", "Healthcare.Domain.csproj")))
            {
                // Prefer directory that contains Domain project
                if (Directory.Exists(Path.Combine(dir.FullName, "Healthcare.Domain")))
                    return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate solution root containing Healthcare.Domain from " + AppContext.BaseDirectory);
    }

    #endregion
}
