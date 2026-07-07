using Healthcare.Adapters.Events;
using Healthcare.Domain.Common;
using Healthcare.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Healthcare.Adapters.Persistence.EntityFramework;

public class HealthcareDbContext : DbContext
{
    private readonly OutboxSettings? _outboxSettings;

    public HealthcareDbContext(DbContextOptions<HealthcareDbContext> options)
        : base(options)
    {
    }

    public HealthcareDbContext(DbContextOptions<HealthcareDbContext> options, OutboxSettings outboxSettings)
        : base(options)
    {
        _outboxSettings = outboxSettings;
    }

    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HealthcareDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_outboxSettings?.UseOutboxForDomainEvents == true)
        {
            var outboxRows = new List<OutboxMessage>();

            foreach (var entry in ChangeTracker.Entries<Entity>())
            {
                var entity = entry.Entity;
                if (entity.DomainEvents.Count == 0)
                    continue;

                foreach (var domainEvent in entity.DomainEvents)
                {
                    var eventType = domainEvent.GetType().AssemblyQualifiedName!;
                    var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
                    outboxRows.Add(new OutboxMessage(eventType, payload, domainEvent.OccurredOn));
                }

                entity.ClearDomainEvents();
            }

            if (outboxRows.Count > 0)
            {
                OutboxMessages.AddRange(outboxRows);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
