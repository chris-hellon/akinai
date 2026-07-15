using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTotalsAndCurrencyTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                schema: "App",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                schema: "App",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                schema: "App",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "InvoiceCurrencyTotals",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceCurrencyTotals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceCurrencyTotals_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "App",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceCurrencyTotals_Currency",
                schema: "App",
                table: "InvoiceCurrencyTotals",
                column: "Currency");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceCurrencyTotals_InvoiceId_Currency",
                schema: "App",
                table: "InvoiceCurrencyTotals",
                columns: new[] { "InvoiceId", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceCurrencyTotals",
                schema: "App");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                schema: "App",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                schema: "App",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Total",
                schema: "App",
                table: "Invoices");
        }
    }
}
