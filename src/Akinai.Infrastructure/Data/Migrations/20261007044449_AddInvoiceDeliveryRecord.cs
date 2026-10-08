using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDeliveryRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SentAtUtc",
                schema: "App",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentDocumentSha256",
                schema: "App",
                table: "Invoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentToEmail",
                schema: "App",
                table: "Invoices",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentAtUtc",
                schema: "App",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SentDocumentSha256",
                schema: "App",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SentToEmail",
                schema: "App",
                table: "Invoices");
        }
    }
}
