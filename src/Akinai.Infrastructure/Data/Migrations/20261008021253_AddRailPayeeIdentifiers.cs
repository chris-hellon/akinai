using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRailPayeeIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PixKey",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(77)",
                maxLength: 77,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptPayId",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiVpa",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PixKey",
                schema: "App",
                table: "CurrencyPaymentSettings");

            migrationBuilder.DropColumn(
                name: "PromptPayId",
                schema: "App",
                table: "CurrencyPaymentSettings");

            migrationBuilder.DropColumn(
                name: "UpiVpa",
                schema: "App",
                table: "CurrencyPaymentSettings");
        }
    }
}
