using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryCodesAndClientTaxNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCountryCode",
                schema: "App",
                table: "Clients",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBusiness",
                schema: "App",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                schema: "App",
                table: "Clients",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressCountryCode",
                schema: "App",
                table: "BusinessProfiles",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingCountryCode",
                schema: "App",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "IsBusiness",
                schema: "App",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                schema: "App",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AddressCountryCode",
                schema: "App",
                table: "BusinessProfiles");
        }
    }
}
