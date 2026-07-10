namespace Healthcare.Presentation.API.Services;

/// <summary>
/// Configuration for database initialization, demo seeding, and first-admin bootstrap.
/// Bind from the <c>Seeding</c> configuration section (and optional legacy top-level flags).
/// </summary>
public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    /// <summary>
    /// When true, seed sample doctors / demo entities — only if the environment policy allows it.
    /// Production always blocks demo seeding regardless of this flag.
    /// </summary>
    public bool SeedDemoData { get; set; }

    /// <summary>
    /// Explicit opt-in to seed demo data outside Development (e.g. local Docker with
    /// <c>ASPNETCORE_ENVIRONMENT=Docker</c>). Has no effect in Production.
    /// </summary>
    public bool AllowDemoDataOutsideDevelopment { get; set; }

    /// <summary>
    /// Secure bootstrap of the first Admin user when none exists.
    /// Credentials must come from environment variables / secret stores — never commit real passwords.
    /// </summary>
    public BootstrapAdminOptions BootstrapAdmin { get; set; } = new();
}

/// <summary>
/// Options for creating the first Admin user (idempotent: skipped when any Admin already exists).
/// </summary>
public sealed class BootstrapAdminOptions
{
    /// <summary>
    /// When true, attempt to create the first admin if no Admin-role user exists.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Admin username. Default: <c>admin</c>.</summary>
    public string Username { get; set; } = "admin";

    /// <summary>Admin email address. Required when <see cref="Enabled"/> is true.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Admin password from secrets/env (e.g. <c>Seeding__BootstrapAdmin__Password</c>).
    /// Never log this value. In non-Production environments, if empty, a random password
    /// may be generated once and written to the log (Development/local only).
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
