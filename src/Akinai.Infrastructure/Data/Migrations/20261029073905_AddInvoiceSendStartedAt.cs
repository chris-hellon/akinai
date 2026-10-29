using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceSendStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SendStartedAtUtc",
                schema: "App",
                table: "Invoices",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SendStartedAtUtc",
                schema: "App",
                table: "Invoices");
        }
    }
}
