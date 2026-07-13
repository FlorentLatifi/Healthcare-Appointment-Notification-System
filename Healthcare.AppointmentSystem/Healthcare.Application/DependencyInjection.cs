using System.Reflection;
using FluentValidation;
using Healthcare.Application.Behaviors;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Audit;
using Healthcare.Application.Services;
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

        // Default no-op audit context; Presentation replaces with HttpAuditContext.
        services.AddSingleton<IAuditContext>(NullAuditContext.Instance);
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        services.AddValidatorsFromAssembly(assembly);

        // Pipeline order: first registered = outermost.
        // Logging → Metrics → Performance → Validation → Audit → Transaction → Handler
        // Audit is outside Transaction so failure audits are not rolled back with the business txn.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
