using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropVndToDefaultCurrencyRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VndToDefaultCurrencyRate",
                schema: "App",
                table: "BusinessProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VndToDefaultCurrencyRate",
                schema: "App",
                table: "BusinessProfiles",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);
        }
    }
}
