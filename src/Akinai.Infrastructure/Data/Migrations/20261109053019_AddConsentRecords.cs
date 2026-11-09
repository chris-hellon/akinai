using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    DocumentVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserId_Purpose_RecordedAtUtc",
                schema: "App",
                table: "ConsentRecords",
                columns: new[] { "UserId", "Purpose", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords",
                schema: "App");
        }
    }
}
