using FluentAssertions;
using Healthcare.Adapters.Persistence.EntityFramework;
using Healthcare.Adapters.Persistence.EntityFramework.Interceptors;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.UnitTests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Healthcare.UnitTests.Adapters.Persistence;

public sealed class AuditLogAppendOnlyInterceptorTests
{
    private static async Task<(SqliteConnection connection, HealthcareDbContext context)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var interceptor = new AuditLogAppendOnlyInterceptor();
        var options = new DbContextOptionsBuilder<HealthcareDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        var context = new SqliteCompatibleDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return (connection, context);
    }

    [Fact]
    public async Task SaveChanges_WithInterceptor_AllowsInsert()
    {
        var (conn, ctx) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = ctx;

        ctx.AuditLogs.Add(AuditLogEntry.Create(
            "TestAction", "Patient", 1, AuditOutcome.Success, 1, "Admin",
            "127.0.0.1", "c1", "test", "{}"));

        var count = await ctx.SaveChangesAsync();
        count.Should().Be(1);
        (await ctx.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveChanges_WithInterceptor_RejectsDelete()
    {
        var (conn, ctx) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = ctx;

        var entry = AuditLogEntry.Create(
            "TestAction", "Patient", 1, AuditOutcome.Success, 1, "Admin",
            "127.0.0.1", "c1", "test", "{}");
        ctx.AuditLogs.Add(entry);
        await ctx.SaveChangesAsync();

        ctx.AuditLogs.Remove(entry);
        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public async Task SaveChanges_WithInterceptor_RejectsUpdate()
    {
        var (conn, ctx) = await CreateContextAsync();
        await using var _ = conn;
        await using var __ = ctx;

        var entry = AuditLogEntry.Create(
            "TestAction", "Patient", 1, AuditOutcome.Success, 1, "Admin",
            "127.0.0.1", "c1", "test", "{}");
        ctx.AuditLogs.Add(entry);
        await ctx.SaveChangesAsync();

        ctx.Entry(entry).State = EntityState.Modified;
        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*append-only*");
    }
}
