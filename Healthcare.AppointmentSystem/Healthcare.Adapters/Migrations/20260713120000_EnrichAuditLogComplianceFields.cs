using Healthcare.Adapters.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Healthcare.Adapters.Migrations;

/// <summary>
/// Adds compliance fields to AuditLogs (actor role, outcome, client IP, correlation id, user agent).
/// Must carry <see cref="MigrationAttribute"/> so <c>Database.MigrateAsync</c> discovers it.
/// </summary>
[DbContext(typeof(HealthcareDbContext))]
[Migration("20260713120000_EnrichAuditLogComplianceFields")]
public partial class EnrichAuditLogComplianceFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActorRole",
            table: "AuditLogs",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Outcome",
            table: "AuditLogs",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Success");

        migrationBuilder.AddColumn<string>(
            name: "ClientIp",
            table: "AuditLogs",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CorrelationId",
            table: "AuditLogs",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UserAgent",
            table: "AuditLogs",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_UserId",
            table: "AuditLogs",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_CorrelationId",
            table: "AuditLogs",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Outcome",
            table: "AuditLogs",
            column: "Outcome");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_UserId",
            table: "AuditLogs");

        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_CorrelationId",
            table: "AuditLogs");

        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_Outcome",
            table: "AuditLogs");

        migrationBuilder.DropColumn(
            name: "ActorRole",
            table: "AuditLogs");

        migrationBuilder.DropColumn(
            name: "Outcome",
            table: "AuditLogs");

        migrationBuilder.DropColumn(
            name: "ClientIp",
            table: "AuditLogs");

        migrationBuilder.DropColumn(
            name: "CorrelationId",
            table: "AuditLogs");

        migrationBuilder.DropColumn(
            name: "UserAgent",
            table: "AuditLogs");
    }
}
