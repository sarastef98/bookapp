using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookFlow.Api.Migrations;

/// <inheritdoc />
public partial class _20261113141425_AddBookings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Bookings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Start = table.Column<long>(type: "INTEGER", nullable: false),
                End = table.Column<long>(type: "INTEGER", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Bookings", x => x.Id);
                table.ForeignKey(
                    name: "FK_Bookings_Resources_ResourceId",
                    column: x => x.ResourceId,
                    principalTable: "Resources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_ResourceId_Status",
            table: "Bookings",
            columns: new[] { "ResourceId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Bookings");
    }
}
