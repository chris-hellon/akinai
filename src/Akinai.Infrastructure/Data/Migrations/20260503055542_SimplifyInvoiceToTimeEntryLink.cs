using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyInvoiceToTimeEntryLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new InvoiceId column to TimeEntries
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "TimeEntries",
                type: "uniqueidentifier",
                nullable: true);

            // Step 2: Migrate data - copy InvoiceId from InvoiceLineItems to TimeEntries
            migrationBuilder.Sql(@"
                UPDATE te
                SET te.InvoiceId = ili.InvoiceId
                FROM TimeEntries te
                INNER JOIN InvoiceLineItems ili ON te.InvoiceLineItemId = ili.Id
            ");

            // Step 3: Drop the old FK and columns
            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_InvoiceLineItems_InvoiceLineItemId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_InvoiceLineItemId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_IsBilled",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "InvoiceLineItemId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "IsBilled",
                table: "TimeEntries");

            // Step 4: Drop InvoiceLineItems table
            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            // Step 5: Drop computed columns from Invoices
            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SubtotalCurrency",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxAmountCurrency",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Invoices");

            migrationBuilder.RenameColumn(
                name: "TotalCurrency",
                table: "Invoices",
                newName: "Currency");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Invoices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            // Step 6: Add index and FK for the new InvoiceId column
            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_InvoiceId",
                table: "TimeEntries",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_Invoices_InvoiceId",
                table: "TimeEntries",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_Invoices_InvoiceId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_InvoiceId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "TimeEntries");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "Invoices",
                newName: "TotalCurrency");

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceLineItemId",
                table: "TimeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBilled",
                table: "TimeEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "TotalCurrency",
                table: "Invoices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "USD");

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SubtotalCurrency",
                table: "Invoices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TaxAmountCurrency",
                table: "Invoices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotalCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_IsBilled",
                table: "TimeEntries",
                column: "IsBilled");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_InvoiceLineItemId",
                table: "TimeEntries",
                column: "InvoiceLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_JobId",
                table: "InvoiceLineItems",
                column: "JobId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_InvoiceLineItems_InvoiceLineItemId",
                table: "TimeEntries",
                column: "InvoiceLineItemId",
                principalTable: "InvoiceLineItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
