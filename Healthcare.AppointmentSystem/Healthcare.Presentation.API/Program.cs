using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Healthcare.Adapters;

using Healthcare.Application.Commands.BookAppointment;
using Healthcare.Application.Commands.CancelAppointment;
using Healthcare.Application.Commands.ConfirmAppointment;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Common;
using Healthcare.Presentation.API.Filters;
using Healthcare.Presentation.API.Middleware;

using Serilog;


// ============================================
// SERILOG CONFIGURATION
// ============================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/healthcare-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Healthcare API...");

    var builder = WebApplication.CreateBuilder(args);

    // ============================================
    // LOGGING
    // ============================================
    builder.Host.UseSerilog();

    // ============================================
    // CONTROLLERS & VALIDATION
    // ============================================
    builder.Services.AddControllers(options =>
    {
        // Add validation filter globally
        options.Filters.Add<ValidationFilter>();
    });


    // FluentValidation
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();


    // ============================================
    // API VERSIONING
    // ============================================
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc() // ← IMPORTANT: Shto këtë
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    
    // ============================================
    // SWAGGER/OPENAPI
    // ============================================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        // API v1 Documentation
        options.SwaggerDoc("v1", new()
        {
            Title = "Healthcare Appointment API",
            Version = "v1.0",
            Description = "RESTful API for managing healthcare appointments, patients, and doctors - Version 1.0",
            Contact = new()
            {
                Name = "Healthcare Team",
                Email = "support@healthcareclinic.com"
            }
        });

        // Future: Add v2 when ready
        // options.SwaggerDoc("v2", new() { ... });

        // Include XML comments for better documentation
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // ============================================
    // APPLICATION LAYER (COMMAND HANDLERS)
    // ============================================
    builder.Services.AddScoped<ICommandHandler<BookAppointmentCommand, Result<int>>, BookAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<ConfirmAppointmentCommand, Result>, ConfirmAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<CancelAppointmentCommand, Result>, CancelAppointmentHandler>();
    builder.Services.AddScoped<ICommandHandler<CreatePatientCommand, Result<int>>, CreatePatientHandler>();

    // ============================================
    // ADAPTERS LAYER
    // ============================================
    // Choose one of these configurations:

    // OPTION 1: Development (Console notifications, In-Memory)
    //builder.Services.AddAdaptersWithInMemoryPersistence();

    // OPTION 2: Production with SQL Server (EF Core)
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddAdaptersWithEFCorePersistence(connectionString);

    // OPTION 2: Production with Email (configure appsettings.json first)
    // var emailSettings = builder.Configuration
    //     .GetSection("Email")
    //     .Get<EmailSettings>();
    // builder.Services.AddAdaptersWithEmail(emailSettings!);

    // OPTION 3: Production with Email + Console
    // builder.Services.AddAdaptersWithCompositeNotifications(emailSettings!);

    // ============================================
    // CORS (if needed for frontend)
    // ============================================
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // ============================================
    // BUILD APP
    // ============================================
    var app = builder.Build();

    // ============================================
    // MIDDLEWARE PIPELINE
    // ============================================

    // Global exception handling
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Swagger in all environments (for demo purposes)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });

    app.UseHttpsRedirection();

    app.UseCors("AllowAll");

    app.UseAuthorization();

    app.MapControllers();

    // ============================================
    // STARTUP LOGGING
    // ============================================
    Log.Information("Healthcare API started successfully");
    Log.Information("Swagger UI available at: {Url}", "https://localhost:7039");
    Log.Information("Using Adapters: In-Memory Persistence + Console Notifications");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
