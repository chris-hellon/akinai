using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSettlementAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FxGainOrLoss",
                schema: "App",
                table: "BankTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementCause",
                schema: "App",
                table: "BankTransactions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnexplainedAmount",
                schema: "App",
                table: "BankTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingAmount",
                schema: "App",
                table: "BankTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WithholdingCertificateReceived",
                schema: "App",
                table: "BankTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingPercentage",
                schema: "App",
                table: "BankTransactions",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FxGainOrLoss",
                schema: "App",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementCause",
                schema: "App",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "UnexplainedAmount",
                schema: "App",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "WithholdingAmount",
                schema: "App",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "WithholdingCertificateReceived",
                schema: "App",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "WithholdingPercentage",
                schema: "App",
                table: "BankTransactions");
        }
    }
}
