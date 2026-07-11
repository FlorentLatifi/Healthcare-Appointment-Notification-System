using System.Reflection;
using FluentValidation;
using Healthcare.Application.Behaviors;
using Healthcare.Application.Observability;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Healthcare.Application;

/// <summary>
/// Application-layer composition root (handlers, validators, MediatR pipeline).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddSingleton<IBusinessMetrics, BusinessMetrics>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        services.AddValidatorsFromAssembly(assembly);

        // Pipeline order: first registered = outermost.
        // Logging → Metrics → Performance → Validation → Transaction → Handler
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
