using Healthcare.Adapters.Common;
using Healthcare.Adapters.Events;
using Healthcare.Adapters.Events.Handlers;
using Healthcare.Adapters.Notifications;
using Healthcare.Adapters.Persistence.InMemory;
using Healthcare.Application.Ports.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Notifications;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Healthcare.Adapters.Authentication;
using Healthcare.Application.Ports.Authentication;
using Microsoft.Extensions.Configuration;
using Healthcare.Domain.Services;
using System.Net.NetworkInformation;
using Healthcare.Adapters.Locking;
using Healthcare.Application.Ports.Locking;
using StackExchange.Redis;
using Healthcare.Adapters.Payments;
using Healthcare.Application.Ports.Payments;
using Healthcare.Adapters.Caching;
using Healthcare.Application.Ports.Caching;
using Healthcare.Adapters.Services;


namespace Healthcare.Adapters;
/// <summary>
/// Extension methods for registering Adapter layer services.
/// </summary>
/// <remarks>
/// 
/// This class centralizes ALL adapter registrations:
/// - Repositories (persistence)
/// - Notification services
/// - Event dispatcher
/// - Event handlers
/// - Time providers
/// 
/// Benefits:
/// - Clean Program.cs
/// - Easy to switch implementations
/// - Single place to manage adapters
/// - Multiple configuration strategies
/// </remarks>
public static class AdapterServiceExtensions
{

    // ✅ NEW: Redis Registration Method
    /// <summary>
    /// Registers Redis distributed locking service.
    /// </summary>
    /// <remarks>
    /// 
    /// Redis Connection Pooling:
    /// - Uses IConnectionMultiplexer (singleton) for connection pooling
    /// - Thread-safe and multiplexed (multiple concurrent operations)
    /// - Auto-reconnects on failures
    /// 
    /// Configuration:
    /// - Reads from appsettings.json → Redis section
    /// - Connection string format: "host:port,abortConnect=false"
    /// 
    /// Usage in Program.cs:
    /// <code>
    /// builder.Services.AddRedisDistributedLocking(builder.Configuration);
    /// </code>
    /// </remarks>
    /// <summary>
    /// Registers Redis distributed locking service.
    /// </summary>
    public static IServiceCollection AddRedisDistributedLocking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ✅ MANUAL BINDING - No Microsoft.Extensions.Configuration.Binder needed
        var redisSection = configuration.GetSection("Redis");

        var redisSettings = new RedisSettings
        {
            ConnectionString = redisSection["ConnectionString"] ?? "localhost:6379",
            InstanceName = redisSection["InstanceName"] ?? "HealthcareApp:",
            DefaultLockExpirationSeconds = int.TryParse(
                redisSection["DefaultLockExpirationSeconds"],
                out var seconds) ? seconds : 30
        };

        services.AddSingleton(redisSettings);

        // Register Redis connection (singleton - connection pooling)
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var settings = provider.GetRequiredService<RedisSettings>();

            var configOptions = ConfigurationOptions.Parse(settings.ConnectionString);
            configOptions.AbortOnConnectFail = false; // Retry on connection failure
            configOptions.ConnectTimeout = 5000; // 5 seconds
            configOptions.SyncTimeout = 5000;

            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Register distributed lock service
        services.AddScoped<IDistributedLockService, RedisDistributedLockService>();

        return services;
    }
    /// <summary>
    /// Registers ALL adapters with in-memory persistence and console notifications.
    /// </summary>
    /// <remarks>
    /// Use this for development and testing.
    /// 
    /// Configuration:
    /// - In-Memory repositories (fast, no setup)
    /// - Console notifications (instant feedback)
    /// - System time provider (real time)
    /// - All event handlers registered
    /// 
    /// Usage in Program.cs:
    /// builder.Services.AddAdaptersWithInMemoryPersistence();
    /// </remarks>
    public static IServiceCollection AddAdaptersWithInMemoryPersistence(
        this IServiceCollection services)
    {
        // Persistence Adapters (In-Memory)
        // Singleton: All requests share same data (simulates database)
        services.AddCoreInMemoryRepositories();
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();

        // Authentication Services
        services.AddSingleton<JwtSettings>(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            return JwtSettings.FromConfiguration(config);
        });

        services.AddScoped<IPasswordHasher, Argon2IdPasswordHasher>();
        services.AddHttpClient<IBreachedPasswordChecker, HaveIBeenPwnedPasswordChecker>();
        services.AddScoped<IAuthenticationService, JwtAuthenticationService>();
        // Notification Adapters (Console)
        // Scoped: New instance per request
        services.AddScoped<INotificationService, ConsoleNotificationAdapter>();

        // Event Infrastructure
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Event Handlers (Observers)
        RegisterEventHandlers(services);

        // Time Provider (System)
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        services.AddSingleton<IDistributedLockService, InMemoryLockService>();

        services.AddSingleton<IAppointmentCodeGenerator>(
            _ => AppointmentCodeGenerator.Instance);

        // Register Redis for refresh token storage (gracefully handles missing Redis)
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var connectionString = config.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectTimeout = 5000;
            configOptions.SyncTimeout = 5000;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Doctor Cache (in-memory for dev/test)
        services.AddSingleton<IDoctorCacheService, InMemoryDoctorCacheService>();

        return services;
    }

    /// <summary>
    /// Registers adapters with email notifications (production).
    /// </summary>
    /// <remarks>
    /// Use this for production with real SMTP emails.
    /// 
    /// Configuration:
    /// - In-Memory repositories (can be replaced with EF Core)
    /// - Email notifications via SMTP
    /// - System time provider
    /// 
    /// Usage:
    /// var emailSettings = builder.Configuration
    ///     .GetSection("Email")
    ///     .Get<EmailSettings>();
    /// builder.Services.AddAdaptersWithEmail(emailSettings);
    /// </remarks>
    public static IServiceCollection AddAdaptersWithEmail(
        this IServiceCollection services,
        EmailSettings emailSettings)
    {
        // Persistence (still in-memory for now)
        services.AddCoreInMemoryRepositories();

        // Email Notification Adapter
        services.AddSingleton(emailSettings);
        services.AddScoped<INotificationService, EmailNotificationAdapter>();

        // Event Infrastructure
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        RegisterEventHandlers(services);

        // Time Provider
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        return services;
    }

    /// <summary>
    /// Registers adapters with composite notifications (email + console).
    /// </summary>
    /// <remarks>
    /// Use this for production with redundancy.
    /// 
    /// Sends notifications via BOTH email AND console.
    /// If email fails, console still works (resilience).
    /// 
    /// Usage:
    /// var emailSettings = builder.Configuration
    ///     .GetSection("Email")
    ///     .Get<EmailSettings>();
    /// builder.Services.AddAdaptersWithCompositeNotifications(emailSettings);
    /// </remarks>
    public static IServiceCollection AddAdaptersWithCompositeNotifications(
        this IServiceCollection services,
        EmailSettings emailSettings)
    {
        // Persistence
        services.AddCoreInMemoryRepositories();

        // Composite Notification (Console + Email)
        services.AddSingleton(emailSettings);
        services.AddScoped<INotificationService>(provider =>
        {
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CompositeNotificationAdapter>>();
            var emailLogger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EmailNotificationAdapter>>();

            return new CompositeNotificationAdapter(
                logger,
                new ConsoleNotificationAdapter(),
                new EmailNotificationAdapter(emailSettings, emailLogger));
        });

        // Event Infrastructure
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        RegisterEventHandlers(services);

        // Time Provider
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        return services;
    }

    /// <summary>
    /// Registers adapters for testing (fake time, console notifications).
    /// </summary>
    /// <remarks>
    /// Use this in unit/integration tests.
    /// 
    /// Configuration:
    /// - In-Memory repositories
    /// - Console notifications (fast)
    /// - Fake time provider (controllable)
    /// 
    /// Usage in tests:
    /// var fakeTime = new FakeTimeProvider(new DateTime(2025, 1, 15));
    /// services.AddAdaptersForTesting(fakeTime);
    /// 
    /// // Now advance time in tests
    /// fakeTime.AdvanceHours(24);
    /// </remarks>
    public static IServiceCollection AddAdaptersForTesting(
        this IServiceCollection services,
        FakeTimeProvider? fakeTimeProvider = null)
    {
        // Persistence
        services.AddCoreInMemoryRepositories();

        // Notification Adapters (Console only for testing)
        services.AddScoped<INotificationService, ConsoleNotificationAdapter>();

        // Event Infrastructure
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        RegisterEventHandlers(services);

        // Fake Time Provider for testing
        var timeProvider = fakeTimeProvider ?? new FakeTimeProvider();
        services.AddSingleton<ITimeProvider>(timeProvider);

        services.AddSingleton<IDistributedLockService, InMemoryLockService>();

        return services;
    }

    // ADD THIS METHOD to AdapterServiceExtensions.cs

    /// <summary>
    /// Registers Stripe payment gateway.
    /// </summary>
    /// <remarks>
    /// 
    /// Configuration:
    /// - Reads from appsettings.json → Stripe section
    /// - Registers IPaymentGateway PORT with StripePaymentGateway ADAPTER
    /// 
    /// Usage in Program.cs:
    /// <code>
    /// builder.Services.AddStripePaymentGateway(builder.Configuration);
    /// </code>
    /// 
    /// Security:
    /// - Never commit real Stripe keys to source control
    /// - Use User Secrets in development
    /// - Use Azure Key Vault / AWS Secrets Manager in production
    /// </remarks>
    public static IServiceCollection AddStripePaymentGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Manual binding - no Microsoft.Extensions.Configuration.Binder needed
        var stripeSection = configuration.GetSection("Stripe");

        var secretKey = stripeSection["SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Stripe secret key is not configured. Set 'Stripe:SecretKey' via environment variables, " +
                "dotnet user-secrets (development), or a secure configuration provider.");
        }

        var publishableKey = stripeSection["PublishableKey"];
        if (string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new InvalidOperationException(
                "Stripe publishable key is not configured. Set 'Stripe:PublishableKey' via environment variables, " +
                "dotnet user-secrets (development), or a secure configuration provider.");
        }

        var stripeSettings = new StripeSettings
        {
            SecretKey = secretKey,
            PublishableKey = publishableKey,
            WebhookSecret = stripeSection["WebhookSecret"] ?? "",
            DefaultCurrency = stripeSection["DefaultCurrency"] ?? "USD"
        };

        services.AddSingleton(stripeSettings);

        // Register payment gateway
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();

        return services;
    }

    /// <summary>
    /// Registers all domain event handlers.
    /// </summary>
    /// <remarks>
    /// Each event can have MULTIPLE handlers (Observer Pattern).
    /// Add new handlers here as they are created.
    /// 
    /// services.AddScoped<IDomainEventHandler<EventType>, HandlerType>();
    /// 
    /// Important: Use Scoped lifetime for handlers!
    /// - Allows DI of scoped services (repositories, etc.)
    /// - New instance per request
    /// - Proper disposal
    /// </remarks>
    /// 
    /// <summary>
    /// Registers adapters with Entity Framework Core persistence.
    /// </summary>
    /// <remarks>
    /// Use this for production with SQL Server database.
    /// 
    /// Configuration Required:
    /// - Connection string in appsettings.json
    /// - SQL Server instance running
    /// - Database created (via migrations)
    /// 
    /// Usage in Program.cs:
    /// builder.Services.AddAdaptersWithEFCorePersistence(
    ///     builder.Configuration.GetConnectionString("DefaultConnection")!);
    /// </remarks>
    public static IServiceCollection AddAdaptersWithEFCorePersistence(
    this IServiceCollection services,
    string connectionString,
    IConfiguration configuration)
    {
        // Database Context
        services.AddDbContext<HealthcareDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories (EF Core implementations)
        services.AddScoped<IAppointmentRepository, EFCoreAppointmentRepository>();
        services.AddScoped<IPatientRepository, EFCorePatientRepository>();
        services.AddScoped<IDoctorRepository, EFCoreDoctorRepository>();
        services.AddScoped<IUserRepository, EFCoreUserRepository>();
        services.AddScoped<IPaymentRepository, EFCorePaymentRepository>();
        services.AddScoped<IAuditLogRepository, EFCoreAuditLogRepository>();
        services.AddScoped<IUserSessionRepository, EFCoreUserSessionRepository>();
        services.AddScoped<IUnitOfWork, EFCoreUnitOfWork>();

        // ✅ AUTHENTICATION SERVICES (Simplified - JWT registered in Program.cs)
        services.AddScoped<IPasswordHasher, Argon2IdPasswordHasher>();
        services.AddHttpClient<IBreachedPasswordChecker, HaveIBeenPwnedPasswordChecker>();
        services.AddScoped<IAuthenticationService, JwtAuthenticationService>();

        // Redis-backed code generator for multi-instance safety
        services.AddSingleton<IAppointmentCodeGenerator>(provider =>
        {
            var redis = provider.GetRequiredService<IConnectionMultiplexer>();
            return new RedisAppointmentCodeGenerator(redis);
        });

        // PAYMENT GATEWAY
        services.AddStripePaymentGateway(configuration);

        // Notification Service
        services.AddScoped<INotificationService, ConsoleNotificationAdapter>();

        // Event Infrastructure
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        RegisterEventHandlers(services);

        // Time Provider
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        // Redis Distributed Locking
        services.AddRedisDistributedLocking(configuration);

        // Doctor Cache (Redis via IConnectionMultiplexer)
        services.AddSingleton<IDoctorCacheService, RedisDoctorCacheService>();

        return services;
    }

    private static IServiceCollection AddCoreInMemoryRepositories(
        this IServiceCollection services)
    {
        services.AddSingleton<IAppointmentRepository, InMemoryAppointmentRepository>();
        services.AddSingleton<IPatientRepository, InMemoryPatientRepository>();
        services.AddSingleton<IDoctorRepository, InMemoryDoctorRepository>();
        services.AddSingleton<IUserSessionRepository, InMemoryUserSessionRepository>();
        services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
        return services;
    }

    private static void RegisterEventHandlers(IServiceCollection services)
    {
        // AppointmentConfirmedEvent Handlers
        services.AddScoped<IDomainEventHandler<AppointmentConfirmedEvent>,
            SendConfirmationNotificationHandler>();
        services.AddScoped<IDomainEventHandler<AppointmentConfirmedEvent>,
            LogAppointmentConfirmedHandler>();

        // AppointmentCancelledEvent Handlers
        services.AddScoped<IDomainEventHandler<AppointmentCancelledEvent>,
            SendCancellationNotificationHandler>();
        services.AddScoped<IDomainEventHandler<AppointmentCancelledEvent>,
            LogAppointmentCancelledHandler>();

        // AppointmentCreatedEvent Handlers
        services.AddScoped<IDomainEventHandler<AppointmentCreatedEvent>,
            LogAppointmentCreatedHandler>();

        // ⭐ Payment Event Handlers (NEW)
        services.AddScoped<IDomainEventHandler<PaymentSucceededEvent>,
            LogPaymentSucceededHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailedEvent>,
            LogPaymentFailedHandler>();
        services.AddScoped<IDomainEventHandler<PaymentRefundedEvent>,
            LogPaymentRefundedHandler>();

        // Doctor Cache Invalidation Handlers
        services.AddScoped<IDomainEventHandler<DoctorCacheInvalidationNeededEvent>,
            InvalidateDoctorCacheHandler>();

        // Read-Access Audit Handlers
        services.AddScoped<IDomainEventHandler<PatientRecordAccessedEvent>,
            LogPatientRecordAccessedHandler>();

        // Audit Log Handlers

        // TODO: Add more handlers as needed:
        // services.AddScoped<IDomainEventHandler<AppointmentCompletedEvent>, ...>();
        // services.AddScoped<IDomainEventHandler<AppointmentNoShowEvent>, ...>();
        // services.AddScoped<IDomainEventHandler<PatientRegisteredEvent>, ...>();
    }

    /// <summary>
    /// Helper method to add a specific notification strategy.
    /// </summary>
    /// <remarks>
    /// Advanced usage: Manually register a custom notification adapter.
    /// 
    /// Example:
    /// services.AddNotificationStrategy<SmsNotificationAdapter>();
    /// </remarks>
    public static IServiceCollection AddNotificationStrategy<TStrategy>(
        this IServiceCollection services)
        where TStrategy : class, INotificationService
    {
        services.AddScoped<INotificationService, TStrategy>();
        return services;
    }

    /// <summary>
    /// Helper method to replace time provider (useful for testing).
    /// </summary>
    /// <remarks>
    /// Advanced usage: Replace time provider after initial registration.
    /// 
    /// Example:
    /// var fakeTime = new FakeTimeProvider();
    /// services.ReplaceTimeProvider(fakeTime);
    /// </remarks>
    public static IServiceCollection ReplaceTimeProvider(
        this IServiceCollection services,
        ITimeProvider timeProvider)
    {
        // Remove existing registration
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITimeProvider));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        // Add new registration
        services.AddSingleton(timeProvider);
        return services;
    }

}
