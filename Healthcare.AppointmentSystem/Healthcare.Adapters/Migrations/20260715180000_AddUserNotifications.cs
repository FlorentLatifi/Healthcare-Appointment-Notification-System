using Healthcare.Adapters.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Healthcare.Adapters.Migrations;

/// <summary>
/// In-app notification inbox for patients, doctors, and admins.
/// </summary>
[DbContext(typeof(HealthcareDbContext))]
[Migration("20260715180000_AddUserNotifications")]
public partial class AddUserNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserNotifications",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                IsRead = table.Column<bool>(type: "bit", nullable: false),
                Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                RelatedEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                RelatedEntityId = table.Column<int>(type: "int", nullable: true),
                ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserNotifications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserNotifications_User_Created",
            table: "UserNotifications",
            columns: new[] { "UserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UserNotifications_User_Unread",
            table: "UserNotifications",
            columns: new[] { "UserId", "IsRead" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserNotifications");
    }
}
