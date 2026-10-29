using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDunningReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutomaticRemindersEnabled",
                schema: "App",
                table: "BusinessProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DunningReminders",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DunningReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DunningReminders_ClientId_SentAtUtc",
                schema: "App",
                table: "DunningReminders",
                columns: new[] { "ClientId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DunningReminders_InvoiceId",
                schema: "App",
                table: "DunningReminders",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DunningReminders",
                schema: "App");

            migrationBuilder.DropColumn(
                name: "AutomaticRemindersEnabled",
                schema: "App",
                table: "BusinessProfiles");
        }
    }
}
