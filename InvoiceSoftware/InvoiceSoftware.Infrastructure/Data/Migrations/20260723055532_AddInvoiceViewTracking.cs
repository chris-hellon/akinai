using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceViewTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstViewedAtUtc",
                schema: "App",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastViewedAtUtc",
                schema: "App",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                schema: "App",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstViewedAtUtc",
                schema: "App",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastViewedAtUtc",
                schema: "App",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                schema: "App",
                table: "Invoices");
        }
    }
}
