using System;
using Healthcare.Adapters.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Healthcare.Adapters.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(HealthcareDbContext))]
    [Migration("20260711120000_OutboxResilience")]
    public partial class OutboxResilience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "MessageId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing rows get distinct ids so the unique index can be created.
            migrationBuilder.Sql("""
                UPDATE OutboxMessages
                SET MessageId = NEWID()
                WHERE MessageId = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE OutboxMessages
                SET NextAttemptAt = OccurredOn
                WHERE NextAttemptAt = '0001-01-01T00:00:00';
                """);

            // Already processed → Processed
            migrationBuilder.Sql("""
                UPDATE OutboxMessages
                SET Status = 2
                WHERE ProcessedAt IS NOT NULL;
                """);

            // Exhausted retries (legacy linear counter) → DeadLetter
            migrationBuilder.Sql("""
                UPDATE OutboxMessages
                SET Status = 3,
                    DeadLetteredAt = ISNULL(ProcessedAt, SYSUTCDATETIME()),
                    NextAttemptAt = '9999-12-31T23:59:59.9999999'
                WHERE ProcessedAt IS NULL AND RetryCount >= 5;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_MessageId",
                table: "OutboxMessages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttempt",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAt", "OccurredOn" },
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DeadLetter",
                table: "OutboxMessages",
                columns: new[] { "Status", "DeadLetteredAt" },
                filter: "[Status] = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_DeadLetter",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttempt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_MessageId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                table: "OutboxMessages",
                columns: new[] { "OccurredOn", "RetryCount" },
                filter: "[ProcessedAt] IS NULL");
        }
    }
}
