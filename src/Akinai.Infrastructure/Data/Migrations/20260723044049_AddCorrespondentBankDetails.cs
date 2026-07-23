using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrespondentBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAddress",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryAccountNumber",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryBankName",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntermediaryBankSwift",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentPurposeCode",
                schema: "App",
                table: "CurrencyPaymentSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAddress",
                schema: "App",
                table: "CurrencyPaymentSettings");

            migrationBuilder.DropColumn(
                name: "IntermediaryAccountNumber",
                schema: "App",
                table: "CurrencyPaymentSettings");

            migrationBuilder.DropColumn(
                name: "IntermediaryBankName",
                schema: "App",
                table: "CurrencyPaymentSettings");

            migrationBuilder.DropColumn(
                name: "IntermediaryBankSwift",
                schema: "App",
                table: "CurrencyPaymentSettings");

            migrationBuilder.DropColumn(
                name: "PaymentPurposeCode",
                schema: "App",
                table: "CurrencyPaymentSettings");
        }
    }
}
