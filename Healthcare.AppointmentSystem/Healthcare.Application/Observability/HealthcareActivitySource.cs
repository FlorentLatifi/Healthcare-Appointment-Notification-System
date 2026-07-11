using System.Diagnostics;

namespace Healthcare.Application.Observability;

/// <summary>Shared ActivitySource for application-layer spans (MediatR, domain workflows).</summary>
public static class HealthcareActivitySource
{
    public const string Name = "Healthcare.Application";
    public static readonly ActivitySource Instance = new(Name, "1.0.0");
}
